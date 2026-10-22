using System;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// PAM status codes.
/// </summary>
public enum PamStatus {
	/// <summary>
	/// Success.
	/// </summary>
	PAM_SUCCESS = 0,

	/// <summary>
	/// dlopen() failure when loading a module.
	/// </summary>
	PAM_OPEN_ERR = 1,

	/// <summary>
	/// Symbol not found in module.
	/// </summary>
	PAM_SYMBOL_ERR = 2,

	/// <summary>
	/// Error in service module.
	/// </summary>
	PAM_SERVICE_ERR = 3,

	/// <summary>
	/// System error.
	/// </summary>
	PAM_SYSTEM_ERR = 4,

	/// <summary>
	/// Memory buffer error.
	/// </summary>
	PAM_BUF_ERR = 5,

	/// <summary>
	/// Permission denied.
	/// </summary>
	PAM_PERM_DENIED = 6,

	/// <summary>
	/// Authentication failure.
	/// </summary>
	PAM_AUTH_ERR = 7,

	/// <summary>
	/// Can not make credentials sufficient.
	/// </summary>
	PAM_CRED_INSUFFICIENT = 8,

	/// <summary>
	/// Underlying authentication service cannot be retrieved.
	/// </summary>
	PAM_AUTHINFO_UNAVAIL = 9,

	/// <summary>
	/// User not known to the underlying authentication module.
	/// </summary>
	PAM_USER_UNKNOWN = 10,

	/// <summary>
	/// Have exhausted maximum number of tries.
	/// </summary>
	PAM_MAXTRIES = 11,

	/// <summary>
	/// Token expired; new authentication token required.
	/// </summary>
	PAM_NEW_AUTHTOK_REQD = 12,

	/// <summary>
	/// User account has expired.
	/// </summary>
	PAM_ACCT_EXPIRED = 13,

	/// <summary>
	/// Cannot establish/maintain the user session.
	/// </summary>
	PAM_SESSION_ERR = 14,

	/// <summary>
	/// Insufficient credentials to access authentication data.
	/// </summary>
	PAM_CRED_UNAVAIL = 15,

	/// <summary>
	/// Credentials expired.
	/// </summary>
	PAM_CRED_EXPIRED = 16,

	/// <summary>
	/// Failure setting user credentials.
	/// </summary>
	PAM_CRED_ERR = 17,

	/// <summary>
	/// No module-specific data is present.
	/// </summary>
	PAM_NO_MODULE_DATA = 18,

	/// <summary>
	/// Conversation error.
	/// </summary>
	PAM_CONV_ERR = 19,

	/// <summary>
	/// Authentication token manipulation error.
	/// </summary>
	PAM_AUTHTOK_ERR = 20,

	/// <summary>
	/// Authentication information recovery error.
	/// </summary>
	PAM_AUTHTOK_RECOVERY_ERR = 21,

	/// <summary>
	/// Authentication token lock busy.
	/// </summary>
	PAM_AUTHTOK_LOCK_BUSY = 22,

	/// <summary>
	/// Authentication token aging disabled.
	/// </summary>
	PAM_AUTHTOK_DISABLE_AGING = 23,

	/// <summary>
	/// Preliminary check by module failed.
	/// </summary>
	PAM_TRY_AGAIN = 24,

	/// <summary>
	/// Ignore underlying module/account check.
	/// </summary>
	PAM_IGNORE = 25,

	/// <summary>
	/// Critical error detected / aborting module.
	/// </summary>
	PAM_ABORT = 26,

	/// <summary>
	/// New authentication token expired.
	/// </summary>
	PAM_AUTHTOK_EXPIRED = 27,

	/// <summary>
	/// Module is unknown.
	/// </summary>
	PAM_MODULE_UNKNOWN = 28,

	/// <summary>
	/// Bad item passed to a pam function.
	/// </summary>
	PAM_BAD_ITEM = 29,

	/// <summary>
	/// Conversation function needs to be called again.
	/// </summary>
	PAM_CONV_AGAIN = 30,

	/// <summary>
	/// Application has to call a PAM function again.
	/// </summary>
	PAM_INCOMPLETE = 31,
} // PamStatus

/// <summary>
/// PAM item types.
/// </summary>
public enum PamItemType {
	/// <summary>
	/// The service name (which identifies that PAM stack that the PAM functions will use to authenticate the program).
	/// </summary>
	PAM_SERVICE = 1,

	/// <summary>
	/// The username of the entity under whose identity service will be given. That is, following authentication,
	/// PAM_USER identifies the local entity that gets to use the service.
	/// Note, this value can be mapped from something (eg., "anonymous") to something else (eg. "guest119") by any
	/// module in the PAM stack. As such an application should consult the value of PAM_USER after each call to a PAM
	/// function.
	/// </summary>
	PAM_USER = 2,

	/// <summary>
	/// The terminal name prefixed by "/dev/" for device files.
	/// </summary>
	PAM_TTY = 3,

	/// <summary>
	/// The requesting user name: local name for a locally requesting user or a remote user name for a remote requesting user.
	/// Generally an application or module will attempt to supply the value that is most strongly authenticated (a local
	/// account before a remote one. The level of trust in this value is embodied in the actual authentication stack
	/// associated with the application, so it is ultimately at the discretion of the system administrator.
	///
	/// PAM_RUSER@PAM_RHOST should always identify the requesting user. In some cases, PAM_RUSER may be NULL. In such
	/// situations, it is unclear who the requesting entity is.
	/// </summary>
	PAM_RUSER = 4,

	/// <summary>
	/// The requesting hostname (the hostname of the machine from which the PAM_RUSER entity is requesting service).
	/// That is PAM_RUSER@PAM_RHOST does identify the requesting user. In some applications, PAM_RHOST may be NULL.
	/// In such situations, it is unclear where the authentication request is originating from.
	/// </summary>
	PAM_RHOST = 5,

	/// <summary>
	/// The authentication token (often a password). This token should be ignored by all module functions besides
	/// pam_sm_authenticate(3) and pam_sm_chauthtok(3). In the former function it is used to pass the most recent
	/// authentication token from one stacked module to another. In the latter function the token is used for another
	/// purpose. It contains the currently active authentication token.
	/// </summary>
	PAM_AUTHTOK = 6,

	/// <summary>
	/// The old authentication token. This token should be ignored by all module functions except pam_sm_chauthtok(3).
	/// </summary>
	PAM_OLDAUTHTOK = 7,

	/// <summary>
	/// The pam_conv structure. See pam_conv(3).
	/// </summary>
	PAM_CONV = 5,

	/// <summary>
	/// The string used when prompting for a user's name. The default value for this string is a localized version of "login: ".
	/// </summary>
	PAM_USER_PROMPT = 9,

	/// <summary>
	/// A function pointer to redirect centrally managed failure delays. See pam_fail_delay(3).
	/// This is specific to Linux-PAM and should not be used in portable applications.
	/// </summary>
	PAM_FAIL_DELAY = 10,

	/// <summary>
	/// The name of the X display. For graphical, X-based applications the value for this item should be the $DISPLAY
	/// variable. This value may be used independently of PAM_TTY for passing the name of the display.
	/// This is specific to Linux-PAM and should not be used in portable applications.
	/// </summary>
	PAM_XDISPLAY = 11,

	/// <summary>
	/// A pointer to a structure containing the X authentication data required to make a connection to the display
	/// specified by PAM_XDISPLAY, if such information is necessary. See pam_xauth_data(3).
	/// This is specific to Linux-PAM and should not be used in portable applications.
	/// </summary>
	PAM_XAUTHDATA = 12,

	/// <summary>
	/// The default action is for the module to use the following prompts when requesting passwords: "New UNIX password: "
	/// and "Retype UNIX password: ". The example word UNIX can be replaced with this item, by default it is empty.
	/// This item is used by pam_get_authtok(3).
	///
	/// If a service module wishes to obtain the name of the user, it should not use this function, but instead perform
	/// a call to pam_get_user(3).
	///
	/// Only a service module is privileged to read the authentication tokens, PAM_AUTHTOK and PAM_OLDAUTHTOK.
	///
	/// This is specific to Linux-PAM and should not be used in portable applications.
	/// </summary>
	PAM_AUTHTOK_TYPE = 13,

} // PamItemType

/// <summary>
/// PAM flags.
/// </summary>
[Flags]
public enum PamFlags {
	/// <summary>
	/// Disallow authentication with a null authentication token.
	/// </summary>
	PAM_DISALLOW_NULL_AUTHTOK = 0x0001,

	/// <summary>
	/// Establish user credentials.
	/// </summary>
	PAM_ESTABLISH_CRED = 0x0002,

	/// <summary>
	/// Delete user credentials.
	/// </summary>
	PAM_DELETE_CRED = 0x0004,

	/// <summary>
	/// Reinitialize user credentials.
	/// </summary>
	PAM_REINITIALIZE_CRED = 0x0008,

	/// <summary>
	/// Refresh user credentials.
	/// </summary>
	PAM_REFRESH_CRED = 0x0010,

	/// <summary>
	/// Change expired authentication token.
	/// </summary>
	PAM_CHANGE_EXPIRED_AUTHTOK = 0x0020,

	/// <summary>
	/// Update authentication token.
	/// </summary>
	PAM_UPDATE_AUTHTOK = 0x2000,

	/// <summary>
	/// Perform preliminary check.
	/// </summary>
	PAM_PRELIM_CHECK = 0x4000,

	/// <summary>
	/// Be quiet; do not emit messages or prompt the user.
	/// </summary>
	PAM_SILENT = 0x8000,
} // PamFlags

public enum PamDataErrorStatus {
	/// <summary>
	/// When a data item is being replaced (through a second call to pam_set_data) this mask is used.
	/// Otherwise, the call is assumed to be from pam_end(3).
	/// </summary>
	PAM_DATA_REPLACE = 0x20000000,

	/// <summary>
	/// This indicates that the process would prefer to perform the cleanup() quietly.
	/// That is, discourages logging/messages to the user. It is generally used to indicate that the current closing of
	/// the library is in a fork(2)ed process, and that the parent will take care of cleaning up things that exist
	/// outside of the current process space (files etc.).
	/// </summary>
	PAM_DATA_SILENT = 0x40000000,
} // PamDataErrorStatus

/// <summary>
/// PAM message styles.
/// See pam_conv(3).
/// </summary>
public enum PamMessageStyle {

	/// <summary>
	/// Prompt the user, with local echo turned off.
	/// Ask for a password.
	/// </summary>
	PAM_PROMPT_ECHO_OFF = 1,

	/// <summary>
	/// Prompt the user, with local echo turned on.
	/// Ask for a username.
	/// </summary>
	PAM_PROMPT_ECHO_ON = 2,

	/// <summary>
	/// Display an error message.
	/// </summary>
	PAM_ERROR_MSG = 3,

	/// <summary>
	/// Display an informational message.
	/// </summary>
	PAM_TEXT_INFO = 4,
} // PamMessageStyle

/// <summary>
/// This is used to prompt the user or display a message to the user, through the PAM communications channel.
/// See <see cref="RpcScandinavia.Repository.PamNss.PromptUser"/>.
/// </summary>
public struct PamUserPrompt {
	public String Key;
	public PamMessageStyle Style;
	public String Message;

	/// <summary>
	/// Instantiates a new PAM user prompt.
	/// </summary>
	/// <param name="key">The identifier of the prompt, used when getting the result.</param>
	/// <param name="style">The kind of user prompt.</param>
	/// <param name="message">The message shown to the user.</param>
	public PamUserPrompt(String key, PamMessageStyle style, String message) {
		this.Key = key;
		this.Style = style;
		this.Message = message;
	} // PamUserPrompt

} // PamUserPrompt
