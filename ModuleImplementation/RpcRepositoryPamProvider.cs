using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Mail;
using System.Reflection;
using Microsoft.Extensions.Logging;
using RpcScandinavia.Repository.Proto;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// Implements missing members that authenticate users, and mange passwords from the RPC Repository using gRPC.
/// </summary>
public class RpcRepositoryPamProvider : PamProviderBase, IDisposable {
	private const String DataKeyUserAccountType = "UserAccountType";
	private const String DataKeyUserShell = "UserShell";
	private const String DataKeyUserFullName = "UserFullName";
	private const String DataKeyUserMail = "UserMail";
	private const String DataKeyUserLanguage = "UserLanguage";
	private const String DataKeyUserLocation = "UserLocation";
	private const String DataKeyUserAvatar = "UserAvatar";
	private readonly Configuration options;

	public RpcRepositoryPamProvider(IntPtr pamh, PamFlags flags, Int32 argc, IntPtr argv) : base(pamh, flags, argc, argv) {
		// Get the configuration.
		this.options = new Configuration();
		this.logLevel = this.options.LogLevel;
	} // RpcRepositoryPamProvider

	/// <inheritdoc/>
	public override void Dispose() {
		base.Dispose();
	} // Dispose

	/// <inheritdoc/>
	protected override PamStatus Authenticate() {
		// Log the configuration.
		// This is only done when authenticating, to limit the logging.
		if (this.logger.IsEnabled(LogLevel.Information) == true) {
			this.LogConfiguration(this.options);
		}

		// Get service name and tty name from PAM.
		String serviceName = this.GetItemString(PamItemType.PAM_SERVICE) ?? String.Empty;
		String ttyName = this.GetItemString(PamItemType.PAM_TTY) ?? String.Empty;

		// Get module and system information.
		Version version = Assembly.GetExecutingAssembly().GetName().Version;
		String versionName = (version != null) ? $"{version.Major}.{version.Minor}.{version.Revision}" : String.Empty;
		String processName = Process.GetCurrentProcess().ProcessName;
		String osName = Environment.OSVersion.Platform.ToString();

		// Log information.
		// This is only done here when authenticating, so limit the logging.
		if (this.logger.IsEnabled(LogLevel.Information) == true) {
			this.logger.LogInformation(
				$$"""
				* UserName: '{{this.userName}}'
				* Tenant: '{{this.options.Tenant}}'
				* Process: '{{processName}}'
				* TTY: '{{ttyName}}'
				* OS: '{{osName}}'
				* Host: '{{this.options.GetHostAndPort}}'
				* Version: '{{versionName}}'
				"""
			);
		}

		// Log certificate information.
		// This is only done here when authenticating, so limit the logging.
		if (this.options.Certificate != null) {
			if (this.logger.IsEnabled(LogLevel.Information) == true) {
				this.logger.LogInformation(
					$$"""
					* Certificate name: '{{this.options.Certificate.FriendlyName}}'
					* Certificate issuer: '{{this.options.Certificate.IssuerName.Name}}'
					* Certificate subject: '{{this.options.Certificate.SubjectName.Name}}'
					* Certificate expires: '{{this.options.Certificate.NotAfter:u}}'
					"""
				);
			}
		}

		// Ignore local user.
		if (this.options.IgnoreLocalUsers == true) {
			Ignore ignore = new Ignore();
			if (ignore.IgnoreUser(this.userName) == true) {
				throw new PamException(PamStatus.PAM_USER_UNKNOWN, LogLevel.Information, $"'{this.userName}' is ignored.");
			}
		}

		// Get the gRPC client.
		using DataClient client = new DataClient(this.logger, [this.options.Tenant], this.options.GetHostAndPort, this.options.Certificate, this.options.PamRetries, 250);

		// Perform a dummy authentication, to get information about 2FA.
// TODO: Add API to server, that can validate a user and return whether 2FA is required for login.
// TODO: The server should not require 2FA if the client don't support it (Plasma Login, SDDM, Gnome etc.).
		PamAuthenticateReply grpcReply = client.PamClient.PamAuthenticate(
			new PamAuthenticateRequest {
				Tenants = { this.options.Tenant },
				UserIdentifier = this.userName,
				UserPassword1 = String.Empty,
				UserPassword2 = String.Empty,
				ModuleVersion = versionName,
				ProcessName = processName,
				ServiceName = serviceName,
				TtyName = ttyName,
				OsName = osName,
			}
		);

		// Get the password from PAM.
		String userPassword1 = this.GetItemString(PamItemType.PAM_AUTHTOK);
		String userPassword2 = String.Empty;

		// Prepare the user prompt.
		// Don't ask for password if it already exist in PAM.
		// Don't ask for 2FA is it isn't required.
		// Don't show information, if the silent flag is present.
		List<PamUserPrompt> messages = new List<PamUserPrompt>();
		if (String.IsNullOrWhiteSpace(userPassword1) == true) {
			messages.Add(new PamUserPrompt("password1", PamMessageStyle.PAM_PROMPT_ECHO_OFF, "Enter password: "));
		}

		if (grpcReply.RequirePassword2 == true) {
			// Only send messages, when silent isn't requested.
			if (this.HasSilentFlag == false) {
				messages.Add(new PamUserPrompt("info1", PamMessageStyle.PAM_TEXT_INFO, "2FA information here..."));
			}
			messages.Add(new PamUserPrompt("password2", PamMessageStyle.PAM_PROMPT_ECHO_ON, "Enter 2FA: "));

			// Log.
			if (this.logger.IsEnabled(LogLevel.Information) == true) {
				this.logger.LogInformation($"'{this.userName}' requires 2FA TYPE.");
			}
		}

		// Prompt the user, and get the passwords.
		if (messages.Count > 0) {
			Dictionary<String, String> responses = this.PromptUser(messages.ToArray());
			userPassword1 = responses.GetValueOrDefault("password1") ?? String.Empty;
			userPassword2 =	responses.GetValueOrDefault("password2") ?? String.Empty;
		}

		// Perform the authentication through gRPC.
		grpcReply = client.PamClient.PamAuthenticate(
			new PamAuthenticateRequest {
				UserIdentifier = this.userName,
				UserPassword1 = userPassword1,
				UserPassword2 = userPassword2,
				Tenants = { this.options.Tenant },
				ModuleVersion = versionName,
				ProcessName = processName,
				ServiceName = serviceName,
				TtyName = ttyName,
				OsName = osName,
			}
		);

		// Set the password in PAM, so subsequent modules in the stack don't have to ask again.
// TODO: Perhaps do this in the base class.
		this.SetItemString(PamItemType.PAM_AUTHTOK, userPassword1);

		// Set the real username in PAM, if the server returned a canonical username that is different from the
		// username entered, so subsequent modules in the stack use the real username.
		if (String.IsNullOrWhiteSpace(grpcReply.CanonicalUsername) == false) {
			// Log.
			if (this.logger.IsEnabled(LogLevel.Information) == true) {
				this.logger.LogInformation($"'{this.userName}' => '{grpcReply.CanonicalUsername}', updated with canonical username.");
			}

// TODO: Perhaps do this in the base class.
			this.SetItemString(PamItemType.PAM_USER, grpcReply.CanonicalUsername);
			this.userName = grpcReply.CanonicalUsername;
		}

		// Update the module.
		if (grpcReply.ModuleUpgradeAvailable == true) {
			// Get the update data.
			ModuleUpgradeReply grpcUpdateReply = client.PamClient.GetModuleUpgrade(
				new ModuleUpgradeRequest {
					ModuleVersion = versionName,
					ProcessName = processName,
					ServiceName = serviceName,
					TtyName = ttyName,
					OsName = osName,
				}
			);

			// Perform the update.
			if (grpcUpdateReply.Result == true) {
				UpdateHandler updateHandler = new UpdateHandler(this.logger, this.options.UpdateFile, this.options.UpdatePamFile, this.options.UpdateNssFile);
//				updateHandler.Update(this.userName, new Version(grpcUpdateReply.ModuleVersion), grpcUpdateReply.ModuleLibraryChecksum, grpcUpdateReply.ToByteArray());
			}
		}

		// Authentication failed.
		if (grpcReply.Result == false) {
			throw new PamException(PamStatus.PAM_AUTH_ERR, LogLevel.Warning, $"'{this.userName}' not authenticated: {grpcReply.ResultMessage}");
		}

		// Set data in PAM, which are used later in open session.
		this.SetDataInt32(RpcRepositoryPamProvider.DataKeyUserAccountType, grpcReply.UserAccountType);
		this.SetDataString(RpcRepositoryPamProvider.DataKeyUserShell, "/bin/zsh");	// TODO: grpcReply.UserShell);
		this.SetDataString(RpcRepositoryPamProvider.DataKeyUserFullName, grpcReply.UserFullName);
		this.SetDataString(RpcRepositoryPamProvider.DataKeyUserMail, grpcReply.UserMail);
		this.SetDataString(RpcRepositoryPamProvider.DataKeyUserLanguage, grpcReply.UserLanguage);
		this.SetDataString(RpcRepositoryPamProvider.DataKeyUserLocation, grpcReply.UserLocation);
		this.SetDataByteArray(RpcRepositoryPamProvider.DataKeyUserAvatar, grpcReply.UserAvatar.ToByteArray());

		// Log.
		if (this.logger.IsEnabled(LogLevel.Information) == true) {
			this.logger.LogInformation($"'{this.userName}' authenticated.");
		}

		// Return.
		return PamStatus.PAM_SUCCESS;
	} // Authenticate

	/// <inheritdoc/>
	protected override PamStatus OpenSession() {

		// We don't want to fail open session, just because setting a user property fails.
		try {
			// Ignore local user.
			if (this.options.IgnoreLocalUsers == true) {
				Ignore ignore = new Ignore();
				if (ignore.IgnoreUser(this.userName) == true) {
					throw new PamException(PamStatus.PAM_USER_UNKNOWN, LogLevel.Information, $"'{this.userName}' is ignored.");
				}
			}

			// Get data from PAM, which ware set when authenticating.
			Int32 userAccountType = this.GetDataInt32(RpcRepositoryPamProvider.DataKeyUserAccountType);
			String userShell = this.GetDataString(RpcRepositoryPamProvider.DataKeyUserShell);
			String userFullName = this.GetDataString(RpcRepositoryPamProvider.DataKeyUserFullName);
			String userMail = this.GetDataString(RpcRepositoryPamProvider.DataKeyUserMail);
			String userLanguage = this.GetDataString(RpcRepositoryPamProvider.DataKeyUserLanguage);
			String userLocation = this.GetDataString(RpcRepositoryPamProvider.DataKeyUserLocation);
			Byte[] userAvatar = this.GetDataByteArray(RpcRepositoryPamProvider.DataKeyUserAvatar);

			// Log information.
			if (this.logger.IsEnabled(LogLevel.Information) == true) {
				this.logger.LogInformation(
					$$"""
					* AccountType: '{{userAccountType}}'
					* Shell '{{userShell}}'
					* UserName: '{{this.userName}}'
					* FullName: '{{userFullName}}'
					* Mail: '{{userMail}}'
					* Language: '{{userLanguage}}'
					* Location '{{userLocation}}'
					* Avatar: '{{userAvatar?.Length}} bytes.'
					"""
				);
			}

			// Parse the mail address.
			MailAddress.TryCreate(userMail, out MailAddress userMailAddress);

			// Update the user properties.
			// They validate values, catch exceptions, and log errors.
			using DBus dbus = new DBus(this.logger);
			dbus.SetUserValue(this.userName, DBusUserProperty.AccountType, userAccountType);
			dbus.SetUserValue(this.userName, DBusUserProperty.Shell, userShell);
			dbus.SetUserValue(this.userName, DBusUserProperty.FullName, userFullName);
			dbus.SetUserValue(this.userName, DBusUserProperty.Mail, userMailAddress);
			dbus.SetUserValue(this.userName, DBusUserProperty.Language, userLanguage);
			dbus.SetUserValue(this.userName, DBusUserProperty.Location, userLocation);
			dbus.SetUserValue(this.userName, DBusUserProperty.Avatar, userAvatar);
		} catch (Exception exception) {
			// Log.
			if (this.logger.IsEnabled(LogLevel.Error) == true) {
				this.logger.LogError(exception.Message);
			}
		}

		// Return.
		return PamStatus.PAM_SUCCESS;
	} // OpenSession

	/// <inheritdoc/>
	protected override PamStatus ChangePassword() {
		// Get service name and tty name from PAM.
		String serviceName = this.GetItemString(PamItemType.PAM_SERVICE) ?? String.Empty;
		String ttyName = this.GetItemString(PamItemType.PAM_TTY) ?? String.Empty;

		// Get module and system information.
		String processName = Process.GetCurrentProcess().ProcessName;
		String osName = Environment.OSVersion.Platform.ToString();

		// Ignore local user.
		if (this.options.IgnoreLocalUsers == true) {
			Ignore ignore = new Ignore();
			if (ignore.IgnoreUser(this.userName) == true) {
				throw new PamException(PamStatus.PAM_USER_UNKNOWN, LogLevel.Information, $"'{this.userName}' is ignored.");
			}
		}

		// Get the gRPC client.
		using DataClient client = new DataClient(this.logger, [this.options.Tenant], this.options.GetHostAndPort, this.options.Certificate, this.options.PamRetries, 250);

		// Perform preliminary server availability check if requested.
		if (this.HasPreliminaryCheckFlag == true) {
// TODO: Ping the server.
			// Return.
			return PamStatus.PAM_SUCCESS;
		}

		// Perform a dummy authentication, to get information about 2FA.
// TODO: Add API to server, that can validate a user and return whether 2FA is required for change password.
// TODO: The server should not requiere 2FA if the client don't support it (Plasma Login, SDDM, Gnome etc.).
Boolean requierePassword2 = false;

		// Get passwords from PAM.
		String oldUserPassword1 = this.GetItemString(PamItemType.PAM_OLDAUTHTOK) ?? String.Empty;
		String oldUserPassword2 = String.Empty;
		String newUserPassword1 = this.GetItemString(PamItemType.PAM_AUTHTOK) ?? String.Empty;
		String confirmUserPassword1 = String.Empty;

		// Prepare the user prompt.
		// Don't ask for password if it already exists in PAM.
		// Don't ask for 2FA here.
		// Don't show information, if the silent flag is present.
		List<PamUserPrompt> messages = new List<PamUserPrompt>();
		if (String.IsNullOrWhiteSpace(oldUserPassword1) == true) {
			messages.Add(new PamUserPrompt("oldPassword1", PamMessageStyle.PAM_PROMPT_ECHO_OFF, "Enter current password: "));
		}

		if (requierePassword2 == true) {
			// Only send messages, when silent isn't requested.
			if (this.HasSilentFlag == false) {
				messages.Add(new PamUserPrompt("info1", PamMessageStyle.PAM_TEXT_INFO, "2FA information here..."));
			}
			messages.Add(new PamUserPrompt("oldUserPassword2", PamMessageStyle.PAM_PROMPT_ECHO_ON, "Enter 2FA: "));

			// Log.
			if (this.logger.IsEnabled(LogLevel.Information) == true) {
				this.logger.LogInformation($"'{this.userName}' requires 2FA TYPE.");
			}
		}

		if (String.IsNullOrWhiteSpace(newUserPassword1) == true) {
			messages.Add(new PamUserPrompt("newPassword1a", PamMessageStyle.PAM_PROMPT_ECHO_OFF, "Enter new password: "));
			messages.Add(new PamUserPrompt("newPassword1b", PamMessageStyle.PAM_PROMPT_ECHO_OFF, "Confirm new password: "));
		}

		// Prompt the user, and get the passwords.
		if (messages.Count > 0) {
			Dictionary<String, String> responses = this.PromptUser(messages.ToArray());
			oldUserPassword1 =	responses.GetValueOrDefault("oldPassword1") ?? String.Empty;
			oldUserPassword2 =	responses.GetValueOrDefault("oldPassword2") ?? String.Empty;
			newUserPassword1 =	responses.GetValueOrDefault("newPassword1a") ?? String.Empty;
			confirmUserPassword1 =	responses.GetValueOrDefault("newPassword1b") ?? String.Empty;
		}

		// Validate whether the old password is present.
		if (String.IsNullOrWhiteSpace(oldUserPassword1) == true) {
			throw new PamException(PamStatus.PAM_AUTHTOK_RECOVERY_ERR, LogLevel.Warning, $"'{this.userName}' cancel password change. Old password not available.");
		}
		if ((requierePassword2 == true) && (String.IsNullOrWhiteSpace(oldUserPassword2) == true)) {
			throw new PamException(PamStatus.PAM_AUTHTOK_RECOVERY_ERR, LogLevel.Warning, $"'{this.userName}' cancel password change. 2FA password not available.");
		}

		// Validate whether the new password is present.
		if (String.IsNullOrWhiteSpace(newUserPassword1) == true) {
			throw new PamException(PamStatus.PAM_AUTHTOK_RECOVERY_ERR, LogLevel.Warning, $"'{this.userName}' cancel password change. New password not available.");
		}
		if (String.IsNullOrWhiteSpace(confirmUserPassword1) == true) {
			throw new PamException(PamStatus.PAM_AUTHTOK_RECOVERY_ERR, LogLevel.Warning, $"'{this.userName}' cancel password change. Confirmation password not available.");
		}

		// Validate whether the new password equals the confirmation password.
		if (newUserPassword1 != confirmUserPassword1) {
			throw new PamException(PamStatus.PAM_AUTHTOK_RECOVERY_ERR, LogLevel.Warning, $"'{this.userName}' cancel password change. New password and confirmation password don't match.");
		}

		// Perform the change authentication token through gRPC.
		PamChAuthTokReply grpcReply = client.PamClient.PamChAuthTok(
			new PamChAuthTokRequest {
				Tenants = { this.options.Tenant },
				UserIdentifier = this.userName,
				UserPassword1 = oldUserPassword1,
				UserPassword2 = oldUserPassword2,
				UserPasswordNew = newUserPassword1,
				ChangeExpired = false,
				ProcessName = processName,
				ServiceName = serviceName,
				TtyName = ttyName,
// TODO:		OsName = osName,
			}
		);

		// Change authentication token failed.
		if (grpcReply.Result == false) {
			throw new PamException(PamStatus.PAM_AUTHTOK_ERR, LogLevel.Error, $"'{this.userName}' password not changed: {grpcReply.ResultMessage}");
		}

		// Set the password in PAM, so subsequent modules in the stack don't have to ask again.
		this.SetItemString(PamItemType.PAM_OLDAUTHTOK, oldUserPassword1);
		this.SetItemString(PamItemType.PAM_AUTHTOK, newUserPassword1);

		// Log.
		if (this.logger.IsEnabled(LogLevel.Information) == true) {
			this.logger.LogInformation($"'{this.userName}' password was changed.");
		}

		// Return.
		return PamStatus.PAM_SUCCESS;
	} // ChangePassword

	/// <summary>
	/// Write the configuration to the log.
	/// </summary>
	/// <param name="options">The configuration.</param>
	private void LogConfiguration(Configuration options) {
		this.logger.LogInformation(
			$$"""
			RPC Repository PAM/NSS Module Configuration:
			* Host: '{{options.Host}}'
			* Port: '{{options.Port}}'
			* Certificate_file: '{{options.CertificateFile.FullName}}'
			* Certificate_common_name: '{{options.CertificateCommonName}}'
			* Tenant identifier: '{{options.Tenant}}'
			* Log level: '{{options.LogLevel}}'
			* Ignore local users: '{{options.IgnoreLocalUsers}}'
			* Ignore local groups: '{{options.IgnoreLocalGroups}}'
			* PAM retries: '{{options.PamRetries}}'
			* NSS retries: '{{options.NssRetries}}'
			"""
		);
	} // LogConfiguration

} // RpcRepositoryPamProvider
