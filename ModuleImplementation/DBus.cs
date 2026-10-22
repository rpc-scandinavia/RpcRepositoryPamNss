using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tmds.DBus.Protocol;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// DBus client.
/// See <see href="https://github.com/flatpak/ppa-xdg-desktop-portal-gtk/blob/ppa/focal/data/org.freedesktop.Accounts.User.xml"/>.
/// </summary>
public class DBus : IDisposable {
	private readonly ILogger logger;
	private readonly List<FileInfo> deleteFiles;
	private readonly DBusConnection connection;
	private Boolean connected;

	/// <summary>
	/// Instantiates a new DBus client.
	/// </summary>
	/// <param name="pamh">The PAM handle representing the current user authentication session.</param>
	/// <param name="logLevel">The logging level.</param>
	public DBus(ILogger logger) {
		this.logger = logger;
		this.deleteFiles = new List<FileInfo>();

		DBusConnectionOptions options = new DBusConnectionOptions(DBusAddress.System) {
			AutoConnect = true,
		};
		this.connection = new DBusConnection(options);
	} // DBus

	public void Dispose() {
		this.connection.Dispose();
		foreach (FileInfo file in this.deleteFiles) {
			if (file.Exists == true) {
				try {
					file.Delete();
				} catch {}
			}
		}
	} // Dispose

	/// <summary>
	/// Connect to DBus, and ensure this is only done one time.
	/// </summary>
	private async Task EnsureConnectedAsync() {
		if (this.connected == false) {
			await this.connection.ConnectAsync().ConfigureAwait(false);
			this.connected = true;
		}
	} // EnsureConnectedAsync

	/// <summary>
	/// Get the DBus path to the specific username.
	/// </summary>
	/// <param name="userName">The username.</param>
	/// <returns>The path or null.</returns>
	private String GetUserPath(String userName) {
		try {
			this.EnsureConnectedAsync().GetAwaiter().GetResult();

			using MessageWriter writer = this.connection.GetMessageWriter();
			writer.WriteMethodCallHeader(
				destination: "org.freedesktop.Accounts",
				path:        "/org/freedesktop/Accounts",
				@interface:  "org.freedesktop.Accounts",
				signature:   "s",
				member:      "FindUserByName"
			);
			writer.WriteString(userName);

			MessageBuffer message = writer.CreateMessage();

			return this.connection.CallMethodAsync(message, (Message reply, Object state) => {
				if (reply.MessageType == MessageType.Error) {
					if (this.logger.IsEnabled(LogLevel.Error) == true) {
						// Log.
						this.logger.LogError(reply.ErrorNameAsString);
					}

					return null;
				}

				return reply.GetBodyReader().ReadObjectPath().ToString();
			}).GetAwaiter().GetResult();
		} catch (Exception exception) {
			// Log.
			if (this.logger.IsEnabled(LogLevel.Error) == true) {
				this.logger.LogError(exception.Message);
			}

			return null;
		}
	} // GetUserPath

	/// <summary>
	/// Set the specific user value.
	/// This throws an exception if the value is the wrong type.
	/// </summary>
	/// <param name="username">The username.</param>
	/// <param name="property">The property.</param>
	/// <param name="value">The value.</param>
	/// <returns>Whether the user property was set or not.</returns>
	public Boolean SetUserValue(String username, DBusUserProperty property, Object value) {
		try {
			// Validate.
			if (value == null) {
				return false;
			}

			// Get the path to the username.
			String userPath = this.GetUserPath(username);
			if (String.IsNullOrWhiteSpace(userPath) == true) {
				return false;
			}

			String headerDestination = "org.freedesktop.Accounts";
			String headerInterface = "org.freedesktop.Accounts.User";

			this.EnsureConnectedAsync().GetAwaiter().GetResult();

			using MessageWriter writer = this.connection.GetMessageWriter();
			switch (property) {
				// Strings.
				case DBusUserProperty.FullName:
					if (value is not String) {
						throw new ArgumentException($"The value must be an '{typeof(String)}', and not '{value.GetType().Name}'.");
					}
					if (String.IsNullOrWhiteSpace(value as String) == true) {
						throw new ArgumentException($"The value is null or empty.");
					}
					writer.WriteMethodCallHeader(headerDestination, userPath, headerInterface, "SetRealName", "s");
					writer.WriteString((String)value);
				break;

				case DBusUserProperty.Mail:
					if (value is not MailAddress) {
						throw new ArgumentException($"The value must be an '{typeof(MailAddress)}', and not '{value.GetType().Name}'.");
					}
					writer.WriteMethodCallHeader(headerDestination, userPath, headerInterface, "SetEmail", "s");
					writer.WriteString(((MailAddress)value)?.Address ?? String.Empty);
				break;

				case DBusUserProperty.Language:
					if (value is not String) {
						throw new ArgumentException($"The value must be an '{typeof(String)}', and not '{value.GetType().Name}'.");
					}
					if (String.IsNullOrWhiteSpace(value as String) == true) {
						throw new ArgumentException($"The value is null or empty.");
					}
					writer.WriteMethodCallHeader(headerDestination, userPath, headerInterface, "SetLanguage", "s");
					writer.WriteString((String)value);
				break;

				case DBusUserProperty.Location:
					if (value is not String) {
						throw new ArgumentException($"The value must be an '{typeof(String)}', and not '{value.GetType().Name}'.");
					}
					if (String.IsNullOrWhiteSpace(value as String) == true) {
						throw new ArgumentException($"The value is null or empty.");
					}
					writer.WriteMethodCallHeader(headerDestination, userPath, headerInterface, "SetLocation", "s");
					writer.WriteString((String)value);
				break;

				case DBusUserProperty.Shell:
					if (value is not String) {
						throw new ArgumentException($"The value must be an '{typeof(String)}', and not '{value.GetType().Name}'.");
					}
					if (String.IsNullOrWhiteSpace(value as String) == true) {
						throw new ArgumentException($"The value is null or empty.");
					}
					writer.WriteMethodCallHeader(headerDestination, userPath, headerInterface, "SetShell", "s");
					writer.WriteString((String)value);
				break;

				case DBusUserProperty.Home:
					if (value is not String) {
						throw new ArgumentException($"The value must be an '{typeof(String)}', and not '{value.GetType().Name}'.");
					}
					if (String.IsNullOrWhiteSpace(value as String) == true) {
						throw new ArgumentException($"The value is null or empty.");
					}
					writer.WriteMethodCallHeader(headerDestination, userPath, headerInterface, "SetHomeDirectory", "s");
					writer.WriteString((String)value);
				break;

				case DBusUserProperty.Avatar:
					if (value is not Byte[]) {
						throw new ArgumentException($"The value must be an '{typeof(Byte[])}', and not '{value.GetType().Name}'.");
					}

					FileInfo avatarFile = new FileInfo(Path.Join(Path.GetTempPath(), $"/rpc-repository_{username}_{Guid.NewGuid()}.png"));
					this.deleteFiles.Add(avatarFile);
					File.WriteAllBytes(avatarFile.FullName, (Byte[])value ?? []);

					writer.WriteMethodCallHeader(headerDestination, userPath, headerInterface, "SetIconFile", "s");
					writer.WriteString(avatarFile.FullName);
				break;

				case DBusUserProperty.AccountType:
					if (value is not Int32) {
						throw new ArgumentException($"The value must be an '{typeof(Int32)}', and not '{value.GetType().Name}'.");
					}
					if (((Int32)value < 0) || ((Int32)value > 1)){
						throw new ArgumentException($"The value is not 0 or 1.");
					}
					writer.WriteMethodCallHeader(headerDestination, userPath, headerInterface, "SetAccountType", "i");
					writer.WriteInt32((Int32)value);
				break;

				case DBusUserProperty.PasswordMode:
					if (value is not Int32) {
						throw new ArgumentException($"The value must be an '{typeof(Int32)}', and not '{value.GetType().Name}'.");
					}
					if (((Int32)value < 0) || ((Int32)value > 2)){
						throw new ArgumentException($"The value is not 0, 1 or 2.");
					}
					writer.WriteMethodCallHeader(headerDestination, userPath, headerInterface, "SetPasswordMode", "i");
					writer.WriteInt32((Int32)value);
				break;

				case DBusUserProperty.AutomaticLogin:
					if (value is not Boolean) {
						throw new ArgumentException($"The value must be an '{typeof(Boolean)}', and not '{value.GetType().Name}'.");
					}
					writer.WriteMethodCallHeader(headerDestination, userPath, headerInterface, "SetAutomaticLogin", "b");
					writer.WriteBool((Boolean)value);
				break;

				case DBusUserProperty.Locked:
					if (value is not Boolean) {
						throw new ArgumentException($"The value must be an '{typeof(Boolean)}', and not '{value.GetType().Name}'.");
					}
					writer.WriteMethodCallHeader(headerDestination, userPath, headerInterface, "SetLocked", "b");
					writer.WriteBool((Boolean)value);
				break;

				default:
					throw new ArgumentException($"The property '{property}' is not supported.");
			}

			MessageBuffer message = writer.CreateMessage();

			Boolean ok = this.connection.CallMethodAsync(message, (Message reply, Object state) => {
				if (reply.MessageType == MessageType.Error) {
					if (this.logger.IsEnabled(LogLevel.Error) == true) {
						this.logger.LogError(reply.ErrorNameAsString);
					}

					return false;
				}

				return true;
			}).GetAwaiter().GetResult();

			// Log.
			if (this.logger.IsEnabled(LogLevel.Information) == true) {
				this.logger.LogInformation($"'{username}' set user '{property}' to '{value}'");
			}

			return ok;
		} catch (Exception exception) {
			if (this.logger.IsEnabled(LogLevel.Error) == true) {
				this.logger.LogError($"'{username}' set user '{property}' error: {exception.Message}");
			}

			return false;
		}
	} // SetUserValue

} // DBus

/// <summary>
/// The user properties that can be set using DBus.
/// </summary>
public enum DBusUserProperty {

	/// <summary>
	/// Set the users full name.
	/// Value type is <see cref="System.String"/>.
	/// </summary>
	FullName,

	/// <summary>
	/// Set the users e-mail address.
	/// Value type is <see cref="System.String"/>.
	/// </summary>
	Mail,

	/// <summary>
	/// Set the user language.
	/// Example: "de_DE.UTF-8".
	/// Value type is <see cref="System.String"/>.
	/// </summary>
	Language,

	/// <summary>
	/// Set the user location.
	/// Value type is <see cref="System.String"/>.
	/// </summary>
	Location,

	/// <summary>
	/// Set the users shell.
	/// Value type is <see cref="System.String"/>.
	/// </summary>
	Shell,

	/// <summary>
	/// Set the users home directory.
	/// Value type is <see cref="System.String"/>.
	/// </summary>
	Home,

	/// <summary>
	/// Set the user avatar.
	/// The file is copied to the final destination, usually "/var/lib/AccountsService/icons/username".
	/// Value type is an array of <see cref="System.Byte"/>, containing a PNG image.
	/// </summary>
	Avatar,

	/// <summary>
	/// Set the users account type as an integer: 0 = Standard user, 1 = Administrator
	/// Value type is <see cref="System.Int32"/>.
	/// </summary>
	AccountType,

	/// <summary>
	/// The new password mode, encoded as an integer: 0 = Regular password, 1 = Password must be set at next login, 2 = No password
	/// Value type is <see cref="System.Int32"/>.
	/// </summary>
	PasswordMode,

	/// <summary>
	/// Whether to enable automatic login for this user: true/false.
	/// Value type is <see cref="System.Boolean"/>.
	/// </summary>
	AutomaticLogin,

	/// <summary>
	/// Whether to lock or unlock the users account.
	/// Value type is <see cref="System.Boolean"/>.
	/// </summary>
	Locked,

} // DBusUserProperty
