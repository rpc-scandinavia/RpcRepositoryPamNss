using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
namespace RpcScandinavia.Repository.PamNss.Test;

/// <summary>
/// Implements missing members that authenticate users, and mange passwords from hardcoded test data.
/// </summary>
public class TestPamProvider : PamProviderBase, IDisposable {

	public TestPamProvider(IntPtr pamh, PamFlags flags, Int32 argc, IntPtr argv) : base(pamh, flags, argc, argv) {
	} // TestPamProvider

	/// <inheritdoc/>
	public override void Dispose() {
		base.Dispose();
	} // Dispose

	/// <inheritdoc/>
	protected override PamStatus Authenticate() {
		this.logger.LogInformation($"Start authenticate.");

		// Get the user.
		NssUserDto user = TestData.TestUsers.SingleOrDefault((user) => (user.UserName == this.userName));
		if (user == null) {
			this.logger.LogWarning($"User unknown.");
			return PamStatus.PAM_USER_UNKNOWN;
		}

		// Get the password from PAM.
		String password = this.GetItemString(PamItemType.PAM_AUTHTOK);

		// Prompt for the password.
		if (String.IsNullOrWhiteSpace(password) == true) {
			this.logger.LogInformation($"Prompt for the password.");

			List<PamUserPrompt> messages = new List<PamUserPrompt>();
			messages.Add(new PamUserPrompt("password", PamMessageStyle.PAM_PROMPT_ECHO_OFF, "Enter top secret password: "));

			Dictionary<String, String> responses = this.PromptUser(messages.ToArray());
			password = responses.GetValueOrDefault("password") ?? String.Empty;
		}

		// Validate whether the password is empty.
		if (String.IsNullOrWhiteSpace(password) == true) {
			this.logger.LogWarning($"Password is empty.");
			return PamStatus.PAM_TRY_AGAIN;
		}

		// Perform the authentication.
		if ((TestData.Passwords.TryGetValue(this.userName, out String userPassword) == false) ||
			(password != userPassword)) {
			this.logger.LogError($"Password don't match.");
			return PamStatus.PAM_AUTHTOK_ERR;
		}

		// Validate whether the user is expired.
		if (user.ExpiredAt < DateTimeOffset.UtcNow) {
			this.logger.LogWarning($"Account is expired.");
			return PamStatus.PAM_AUTHTOK_EXPIRED;
		}

		// Set the password in PAM, so subsequent modules in the stack don't have to ask again.
// TODO: Perhaps do this in the base class.
		this.SetItemString(PamItemType.PAM_AUTHTOK, password);

		// Ok.
		this.logger.LogInformation($"Successfully authenticated.");
		return PamStatus.PAM_SUCCESS;
	} // Authenticate

} // TestPamProvider
