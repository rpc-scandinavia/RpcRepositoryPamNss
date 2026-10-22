using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using RpcScandinavia.Repository.PamNss.Internal;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// Extend and implement missing members in this base class to provide data for the PAM exports.
/// The following protected fields are available:
///
///  * logger
///  * logLevel
///  * logPrefix
///  * processId
///  * processIsForked
///  * arguments
///  * userName
///
/// The following protected properties are available:
///
///  * HasSilentFlag
///  * HasPreliminaryCheckFlag
///
/// The following protected methods are available:
///
///  * RunProcess
///  * GetUser
///  * GetErrorMessage
///  * GetItemString
///  * SetItemString
///  * GetDataString
///  * SetDataString
///  * GetDataInt32
///  * SetDataInt32
///  * GetDataByteArray
///  * SetDataByteArray
///  * PromptUser
///
/// This base class implements <see cref="Microsoft.Extensions.Logging.ILogger"/>, which writes to the system logger.
///
/// Implement the virtual methods and return the expected result, or throw a <see cref="PamException"/>.
/// The exception is caught and logged before the status is returned.
/// </summary>
public class PamProviderBase : ProviderBase, IDisposable {
	//private static readonly Boolean UsePamLogger = false;
	private static readonly PamDataCleanupFunc CleanupAllocHGlobalDelegate = PamProviderBase.CleanupDataGlobal;
	private static readonly PamDataCleanupFunc CleanupStringDelegate = PamProviderBase.CleanupDataString;
	private const String DataKeyPrefix = "rpc-pam-";
	private readonly IntPtr pamh;
	private readonly PamFlags flags;
	private String funcName;

	#region Constructors and Dispose methods
	//------------------------------------------------------------------------------------------------------------------
	// Constructors and Dispose methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Instantiates a new PAM provider.
	/// </summary>
	public PamProviderBase(IntPtr pamh, PamFlags flags, Int32 argc, IntPtr argv) : base() {
		this.pamh = pamh;
		this.flags = flags;
		this.arguments = this.UnpackStringArray(argc, argv);
		this.funcName = String.Empty;
		this.userName = String.Empty;
	} // PamProviderBase

	/// <inheritdoc/>
	public override void Dispose() {
		base.Dispose();
	} // Dispose
	#endregion

	#region Protected fields.
	//------------------------------------------------------------------------------------------------------------------
	// Protected fields.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Gets the arguments from the PAM configuration file.
	/// </summary>
	protected readonly String[] arguments;

	/// <summary>
	/// Gets or sets the username.
	/// This is initialised by this base class with every PAM request, using <see cref="RpcScandinavia.Repository.PamNss.GetUser"/>.
	/// </summary>
	protected String userName;
	#endregion

	#region Protected properties.
	//------------------------------------------------------------------------------------------------------------------
	// Protected properties.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Gets whether the <see cref="PamFlags.PAM_SILENT"/> flag is set.
	/// </summary>
	protected Boolean HasSilentFlag => ((this.flags & PamFlags.PAM_SILENT) != 0);

	/// <summary>
	/// Gets whether the <see cref="PamFlags.PAM_PRELIM_CHECK"/> flag is set.
	/// </summary>
	protected Boolean HasPreliminaryCheckFlag => ((this.flags & PamFlags.PAM_PRELIM_CHECK) != 0);
	#endregion

	//==================================================================================================================
	// Methods called from the exports.
	//==================================================================================================================

	#region Authentication modules
	//------------------------------------------------------------------------------------------------------------------
	// Authentication modules.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// See <see cref="PamExports.pam_sm_authenticate"/> for full details.
	/// </summary>
	public PamStatus pam_sm_authenticate() {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(PamStatus.PAM_AUTHINFO_UNAVAIL, "authenticate", () => {
			// Return the result from the virtual method.
			return this.Authenticate();
		});
	} // pam_sm_authenticate

	/// <summary>
	/// See <see cref="PamExports.pam_sm_setcred"/> for full details.
	/// </summary>
	public PamStatus pam_sm_setcred() {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(PamStatus.PAM_CRED_ERR, "setcred", () => {
			// Return the result from the virtual method.
			return this.AlterCredentials();
		});
	} // pam_sm_setcred
	#endregion

	#region Account management modules
	//------------------------------------------------------------------------------------------------------------------
	// Account Management modules.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// See <see cref="PamExports.pam_sm_acct_mgmt"/> for full details.
	/// </summary>
	public PamStatus pam_sm_acct_mgmt() {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(PamStatus.PAM_AUTH_ERR, "acct_mgmt", () => {
			// Return the result from the virtual method.
			return this.AccountManagement();
		});
	} // pam_sm_acct_mgmt
	#endregion

	#region Session management modules
	//------------------------------------------------------------------------------------------------------------------
	// Session management modules.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// See <see cref="pam_sm_open_session"/> for full details.
	/// </summary>
	public PamStatus pam_sm_open_session() {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(PamStatus.PAM_SESSION_ERR, "open_session", () => {
			// Return the result from the virtual method.
			return this.OpenSession();
		});
	} // pam_sm_open_session

	/// <summary>
	/// See <see cref="pam_sm_close_session"/> for full details.
	/// </summary>
	public PamStatus pam_sm_close_session() {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(PamStatus.PAM_SESSION_ERR, "close_session", () => {
			// Return the result from the virtual method.
			return this.CloseSession();
		});
	} // pam_sm_close_session
	#endregion

	#region Password management modules
	//------------------------------------------------------------------------------------------------------------------
	// Password management modules.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// See <see cref="PamExports.pam_sm_chauthtok"/> for full details.
	/// </summary>
	public PamStatus pam_sm_chauthtok() {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(PamStatus.PAM_USER_UNKNOWN, "chauthtok", () => {
			// Return the result from the virtual method.
			return this.ChangePassword();
		});
	} // pam_sm_chauthtok
	#endregion

	//==================================================================================================================
	// Virtual methods.
	//==================================================================================================================

	#region Virtual methods
	//------------------------------------------------------------------------------------------------------------------
	// Virtual methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Authenticate the user.
	/// See <see cref="PamExports.pam_sm_authenticate"/> for full details.
	/// </summary>
	protected virtual PamStatus Authenticate() {
		return PamStatus.PAM_USER_UNKNOWN;
	} // Authenticate

	/// <summary>
	/// Alter the user credentials, after the user is authenticated and before the session is opened.
	/// See <see cref="PamExports.pam_sm_setcred"/> for full details.
	/// </summary>
	protected virtual PamStatus AlterCredentials() {
		return PamStatus.PAM_SUCCESS;
	} // AlterCredentials

	/// <summary>
	/// Restrict login other then not authenticating.
	/// This can be used to restrict login based on the time, location etc.
	/// See <see cref="PamExports.pam_sm_acct_mgmt"/> for full details.
	/// </summary>
	protected virtual PamStatus AccountManagement() {
		return PamStatus.PAM_SUCCESS;
	} // AccountManagement

	/// <summary>
	/// Perform tasks when the user session is opened.
	/// See <see cref="pam_sm_open_session"/> for full details.
	/// </summary>
	protected virtual PamStatus OpenSession() {
		return PamStatus.PAM_SUCCESS;
	} // OpenSession

	/// <summary>
	/// Perform tasks when the user session is closed.
	/// See <see cref="pam_sm_close_session"/> for full details.
	/// </summary>
	protected virtual PamStatus CloseSession() {
		return PamStatus.PAM_SUCCESS;
	} // CloseSession

	/// <summary>
	/// Set the user password.
	/// See <see cref="PamExports.pam_sm_chauthtok"/> for full details.
	/// </summary>
	protected virtual PamStatus ChangePassword() {
		return PamStatus.PAM_USER_UNKNOWN;
	} // ChangePassword
	#endregion

	#region Overridden methods
	//------------------------------------------------------------------------------------------------------------------
	// Overridden methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <inheritdoc/>
	protected override sealed String GetLogIdent() {
		return $"PAM [{this.userName} | {this.funcName}]:";
	} // GetLogIdent
	#endregion

	//==================================================================================================================
	// Helper methods.
	//==================================================================================================================

	#region Wrap methods
	//------------------------------------------------------------------------------------------------------------------
	// Wrap methods.
	//------------------------------------------------------------------------------------------------------------------
	private delegate PamStatus WrapDelegate();

	private PamStatus Wrap(PamStatus exceptionResult, String funcName, WrapDelegate func) {
		try {
			// Get the function name, for the logger.
			this.funcName = funcName;

			// Get the username from PAM.
			this.userName = this.GetUser();
			if (String.IsNullOrWhiteSpace(this.userName) == true) {
				throw new PamException(PamStatus.PAM_USER_UNKNOWN, LogLevel.Error);
			}

			// Log.
			if (this.logger.IsEnabled(LogLevel.Debug) == true) {
				this.logger.LogDebug($"Begin.");
			}

			// Call the function.
			PamStatus funcReturn = func();

			// Log.
			if (this.logger.IsEnabled(LogLevel.Debug) == true) {
				this.logger.LogDebug($"End => {funcReturn}");
			}

			// Return.
			return funcReturn;
		} catch (PamException exception) {
			// Log.
			if (this.logger.IsEnabled(exception.LogLevel) == true) {
				this.logger.Log(exception.LogLevel, $"End => {exception.Status}: {exception.Message}");
			}
			if (this.logger.IsEnabled(LogLevel.Trace) == true) {
				this.logger.LogDebug(exception.StackTrace);
			}

			// Return.
			return exception.Status;
		} catch (Exception exception) {
			// Log.
			if (this.logger.IsEnabled(LogLevel.Error) == true) {
				this.logger.LogError($"End => {exceptionResult}: {exception.Message}");
			}
			if (this.logger.IsEnabled(LogLevel.Trace) == true) {
				this.logger.LogDebug(exception.StackTrace);
			}

			// Return.
			return exceptionResult;
		}
	} // Wrap
	#endregion

	#region Unpack methods
	//------------------------------------------------------------------------------------------------------------------
	// Unpack methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Unpacks an unmanaged array of C-style strings (char** argv) into a managed string array.
	/// Used for parsing module arguments defined in PAM configuration files.
	/// </summary>
	/// <param name="argc">The number of arguments in the array.</param>
	/// <param name="argv">Pointer to the array of string pointers (char**).</param>
	/// <returns>A managed array of strings, or an empty array if invalid or empty.</returns>
	private unsafe String[] UnpackStringArray(Int32 argc, IntPtr argv) {
		// Validate.
		if (argc <= 0) {
			return [];
		}
		if (argv == IntPtr.Zero) {
			return [];
		}

		String[] results = new String[argc];
		IntPtr* stringPointers = (IntPtr*)argv;

		for (Int32 index = 0; index < argc; index++) {
			IntPtr strPtr = stringPointers[index];
			if (strPtr != IntPtr.Zero) {
				results[index] = Marshal.PtrToStringUTF8(strPtr) ?? String.Empty;
			} else {
				results[index] = String.Empty;
			}
		}

		return results;
	} // UnpackStringArray
	#endregion

	#region PAM get methods
	//------------------------------------------------------------------------------------------------------------------
	// PAM get methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Get the username.
	/// See <see cref="PamImports.pam_get_user"/> for full details.
	/// </summary>
	/// <param name="prompt">The optional prompt shown to the user, if PAM wasn't initialised with a username.</param>
	/// <returns>The username.</returns>
	protected String GetUser(String prompt = null) {
		PamStatus responseCode = PamImports.pam_get_user(this.pamh, out IntPtr userPointer, prompt);
		if ((responseCode != PamStatus.PAM_SUCCESS) || (userPointer == IntPtr.Zero)) {
			return null;
		}

		return Marshal.PtrToStringUTF8(userPointer);
	} // GetUser

	/// <summary>
	/// Return a string describing a PAM status code.
	/// See <see cref="PamImports.pam_strerror"/> for full details.
	/// </summary>
	/// <param name="statusCode">The status code.</param>
	/// <returns>The error message.</returns>
	public String GetErrorMessage(PamStatus statusCode) {
		IntPtr pointer = PamImports.pam_strerror(this.pamh, statusCode);
		return pointer == (IntPtr.Zero)
			? $"Unknown PAM error: {statusCode}"
			: Marshal.PtrToStringUTF8(pointer);
	} // GetErrorMessage
	#endregion

	#region PAM string item methods
	//------------------------------------------------------------------------------------------------------------------
	// PAM string item methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Get a standard string item from PAM.
	/// See <see cref="PamImports.pam_get_item"/> for full details.
	/// </summary>
	/// <param name="itemType">The item type to get.</param>
	/// <returns>The string or null.</returns>
	protected String GetItemString(PamItemType itemType) {
		// Validate for unsupported types.
		if (itemType == PamItemType.PAM_CONV) {
			throw new NotSupportedException($"'{nameof(PamProviderBase.GetItemString)}' does not handle '{itemType}'. Use '{nameof(PamImports.pam_get_item)}' instead.");
		}
		if (itemType == PamItemType.PAM_USER) {
			throw new NotSupportedException($"'{nameof(PamProviderBase.GetItemString)}' does not handle '{itemType}'. Use '{nameof(this.GetUser)}' instead.");
		}

		// Get the item from PAM.
		PamStatus responseCode = PamImports.pam_get_item(this.pamh, itemType, out IntPtr valuePointer);
		if ((responseCode != PamStatus.PAM_SUCCESS) || (valuePointer == IntPtr.Zero)) {
			return null;
		}

		// Return the string from the pointer.
		// Note, this is a pointer to the actual data and should not be freed or over-written!
		return Marshal.PtrToStringUTF8(valuePointer);
	} // GetItemString

	/// <summary>
	/// Set a standard string item in PAM.
	/// See <see cref="PamImports.pam_set_item"/> for full details.
	/// </summary>
	/// <param name="itemType">The item type to set.</param>
	/// <param name="value">The string value to set.</param>
	/// <returns>The status code.</returns>
	protected PamStatus SetItemString(PamItemType itemType, String value) {
		// Validate for unsupported types.
		if (itemType == PamItemType.PAM_CONV) {
			throw new NotSupportedException($"'{nameof(PamProviderBase.GetItemString)}' does not handle '{itemType}'. Use '{nameof(PamImports.pam_get_item)}' instead.");
		}

		// Create a pointer to the string.
		IntPtr valuePointer = (value is null)
			? IntPtr.Zero
			: Marshal.StringToCoTaskMemUTF8(value);


		// Set the item in PAM.
		// Note a copy of the object pointed to by the item argument is created.
		try {
			return PamImports.pam_set_item(this.pamh, itemType, valuePointer);
		} finally {
			// Free the pointer.
			if (valuePointer != IntPtr.Zero) {
				Marshal.FreeCoTaskMem(valuePointer);
			}
		}
	} // SetItemString
	#endregion

	#region PAM data methods
	//------------------------------------------------------------------------------------------------------------------
	// PAM data methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Get a module string data from PAM.
	/// See <see cref="PamImports.pam_get_data"/> for full details.
	/// </summary>
	/// <param name="key">The data key to get.</param>
	/// <param name="defaultValue">The default value (null).</param>
	/// <returns>The string data or the default value.</returns>
	protected String GetDataString(String key, String defaultValue = null) {
		// Validate.
		if (String.IsNullOrWhiteSpace(key) == true) {
			return defaultValue;
		}

		// Get the data from PAM.
		PamStatus responseCode = PamImports.pam_get_data(this.pamh, $"{PamProviderBase.DataKeyPrefix}{key}", out IntPtr valuePointer);
		if ((responseCode != PamStatus.PAM_SUCCESS) || (valuePointer == IntPtr.Zero)) {
			return defaultValue;
		}

		// Return the string from the pointer.
		// Note, this data is not a copy and should not be freed.
		return Marshal.PtrToStringUTF8(valuePointer);
	} // GetDataString

	/// <summary>
	/// Set a module string data in PAM.
	/// See <see cref="PamImports.pam_set_data"/> for full details.
	/// </summary>
	/// <param name="key">The data key to get.</param>
	/// <param name="value">The string value to set.</param>
	/// <returns>The status code.</returns>
	protected unsafe PamStatus SetDataString(String key, String value) {
		// Validate.
		if (String.IsNullOrWhiteSpace(key) == true) {
			return PamStatus.PAM_BAD_ITEM;
		}

		// Allocate unmanaged memory and write the string.
		IntPtr valuePointer = (value is null)
			? IntPtr.Zero
			: Marshal.StringToCoTaskMemUTF8(value);

		// Set the data in PAM.
		// Note that this functions stores only a pointer to the data, the module should not modify or free the content of it.
		return PamImports.pam_set_data(
			this.pamh,
			$"{PamProviderBase.DataKeyPrefix}{key}",
			valuePointer,
			Marshal.GetFunctionPointerForDelegate(PamProviderBase.CleanupAllocHGlobalDelegate)
		);
	} // SetDataString

	/// <summary>
	/// Get a module integer data from PAM.
	/// See <see cref="PamImports.pam_get_data"/> for full details.
	/// </summary>
	/// <param name="key">The data key to get.</param>
	/// <param name="defaultValue">The default value (0).</param>
	/// <returns>The integer data or the default value.</returns>
	protected Int32 GetDataInt32(String key, Int32 defaultValue = 0) {
		// Validate.
		if (String.IsNullOrWhiteSpace(key) == true) {
			return defaultValue;
		}

		// Get the data from PAM.
		PamStatus responseCode = PamImports.pam_get_data(this.pamh, $"{PamProviderBase.DataKeyPrefix}{key}", out IntPtr valuePointer);
		if ((responseCode != PamStatus.PAM_SUCCESS) || (valuePointer == IntPtr.Zero)) {
			return defaultValue;
		}

		// Return the integer from the pointer.
		// Note, this data is not a copy and should not be freed.
		return Marshal.ReadInt32(valuePointer);
	} // GetDataInt32

	/// <summary>
	/// Set a module integer data in PAM.
	/// See <see cref="PamImports.pam_set_data"/> for full details.
	/// </summary>
	/// <param name="key">The data key to get.</param>
	/// <param name="value">The integer value to set.</param>
	/// <returns>The status code.</returns>
	protected unsafe PamStatus SetDataInt32(String key, Int32 value) {
		// Validate.
		if (String.IsNullOrWhiteSpace(key) == true) {
			return PamStatus.PAM_BAD_ITEM;
		}

		// Allocate unmanaged memory and write the integer.
		IntPtr valuePointer = Marshal.AllocHGlobal(sizeof(Int32));
		Marshal.WriteInt32(valuePointer, value);

		// Set the data in PAM.
		// Note that this functions stores only a pointer to the data, the module should not modify or free the content of it.
		return PamImports.pam_set_data(
			this.pamh,
			$"{PamProviderBase.DataKeyPrefix}{key}",
			valuePointer,
			Marshal.GetFunctionPointerForDelegate(PamProviderBase.CleanupAllocHGlobalDelegate)
		);
	} // SetDataInt32

	/// <summary>
	/// Get a module byte array data from PAM.
	/// See <see cref="PamImports.pam_get_data"/> for full details.
	/// </summary>
	/// <param name="key">The data key to get.</param>
	/// <param name="defaultValue">The default value (null).</param>
	/// <returns>The byte array data or the default value.</returns>
	protected Byte[] GetDataByteArray(String key, Byte[] defaultValue = null) {
		// Validate.
		if (String.IsNullOrWhiteSpace(key) == true) {
			return defaultValue;
		}

		// Get the data from PAM.
		PamStatus responseCode = PamImports.pam_get_data(this.pamh, $"{PamProviderBase.DataKeyPrefix}{key}", out IntPtr valuePointer);
		if ((responseCode != PamStatus.PAM_SUCCESS) || (valuePointer == IntPtr.Zero)) {
			return defaultValue;
		}

		// Get the byte array from the pointer.
		// Note, this data is not a copy and should not be freed.
		// Layout: [int length][bytes…]
		// Read length prefix
		Int32 length = Marshal.ReadInt32(valuePointer);
		if (length <= 0) {
			return Array.Empty<Byte>();
		}

		// Read the bytes.
		Byte[] result = new Byte[length];
		Marshal.Copy(valuePointer + sizeof(Int32), result, 0, length);
		return result;
	} // GetDataByteArray

	/// <summary>
	/// Set a module byte array data in PAM.
	/// See <see cref="PamImports.pam_set_data"/> for full details.
	/// </summary>
	/// <param name="key">The data key to get.</param>
	/// <param name="value">The byte array value to set.</param>
	/// <returns>The status code.</returns>
	protected unsafe PamStatus SetDataByteArray(String key, Byte[] value) {
		// Validate.
		if (String.IsNullOrWhiteSpace(key) == true) {
			return PamStatus.PAM_BAD_ITEM;
		}

		// Allocate unmanaged memory and write the byte array.
		IntPtr valuePointer = IntPtr.Zero;
		if ((value is not null) && (value.Length > 0)) {
			// Layout: [int length][bytes…]
			valuePointer = Marshal.AllocHGlobal(sizeof(Int32) + value.Length);
			Marshal.WriteInt32(valuePointer, value.Length);
			Marshal.Copy(value, 0, valuePointer + sizeof(Int32), value.Length);
		}

		// Set the data in PAM.
		// Note that this functions stores only a pointer to the data, the module should not modify or free the content of it.
		return PamImports.pam_set_data(
			this.pamh,
			$"{PamProviderBase.DataKeyPrefix}{key}",
			valuePointer,
			Marshal.GetFunctionPointerForDelegate(PamProviderBase.CleanupAllocHGlobalDelegate)
		);
	} // SetDataByteArray

	private static void CleanupDataString(IntPtr pamh, IntPtr valuePointer, PamDataErrorStatus error_status) {
		Boolean isReplace = (error_status & PamDataErrorStatus.PAM_DATA_REPLACE) != 0;
		Boolean isSilent  = (error_status & PamDataErrorStatus.PAM_DATA_SILENT)  != 0;

		// Free the pointer.
		if (valuePointer != IntPtr.Zero) {
			// Overwrite the pointer data with zeros (security wipe).
			// This writes a zero, until another zero (end of string) is reached.
			// (requires knowing the length or using a different allocation strategy).
			unsafe {
				Byte* ptr = (Byte*)valuePointer;
				while (*ptr != 0) {
					*ptr = 0;
					ptr++;
				}
			}

			// Free the pointer data.
			Marshal.FreeCoTaskMem(valuePointer);
		}
	} // CleanupDataString

	private static void CleanupDataGlobal(IntPtr pamh, IntPtr valuePointer, PamDataErrorStatus error_status) {
		if (valuePointer != IntPtr.Zero) {
			Marshal.FreeHGlobal(valuePointer);
			// NativeMemory.Free(valuePointer);
		}
	} // CleanupDataGlobal
	#endregion

	#region PAM prompt user methods
	//------------------------------------------------------------------------------------------------------------------
	// PAM prompt user methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Prompt the user with one or more messages and return the responses.
	/// </summary>
	/// <param name="messages">The messages.</param>
	/// <returns>The responses.</returns>
	/// <exception cref="PamException">When it isn't possible to prompt the user.</exception>
	protected Dictionary<String, String> PromptUser(PamUserPrompt[] messages) {
		if ((messages == null) || (messages.Length == 0)) {
			return new Dictionary<String, String>();
		}

		// Get the conversation structure.
		if ((PamImports.pam_get_item(this.pamh, PamItemType.PAM_CONV, out IntPtr conversationPointer) != PamStatus.PAM_SUCCESS) ||
			(conversationPointer == IntPtr.Zero)) {
			throw new PamException(PamStatus.PAM_CONV_ERR, LogLevel.Error);
		}

		pam_conv conversation = Marshal.PtrToStructure<pam_conv>(conversationPointer);
		if (conversation.conv == IntPtr.Zero) {
			throw new PamException(PamStatus.PAM_CONV_ERR, LogLevel.Error);
		}

		PamConvFunc conversationFunction = Marshal.GetDelegateForFunctionPointer<PamConvFunc>(conversation.conv);

		// Filter empty messages and prepare pam_message array.
		PamUserPrompt[] validMessages = messages
			.Where((message) => ((String.IsNullOrWhiteSpace(message.Key) == false) && (String.IsNullOrWhiteSpace(message.Message) == false)))
			.ToArray();

		Int32 validMessageCount = validMessages.Length;
		if (validMessageCount == 0) {
			return new Dictionary<String, String>();
		}

		// Array of pam_message structures.
		IntPtr messagesPointer = Marshal.AllocHGlobal(Marshal.SizeOf<pam_message>() * validMessageCount);

		// Array of pointers to those structures (this is what the C API expects).
		IntPtr messagesPointerArray = Marshal.AllocHGlobal(IntPtr.Size * validMessageCount);

		try {
			for (Int32 validMessageIndex = 0; validMessageIndex < validMessageCount; validMessageIndex++) {
				pam_message message = new pam_message {
					msg_style = validMessages[validMessageIndex].Style,
					msg = Marshal.StringToCoTaskMemUTF8(validMessages[validMessageIndex].Message)
				};

				IntPtr messagePointer = IntPtr.Add(messagesPointer, validMessageIndex * Marshal.SizeOf<pam_message>());
				Marshal.StructureToPtr(message, messagePointer, false);

				// Write the pointer into the pointer-array.
				Marshal.WriteIntPtr(messagesPointerArray, validMessageIndex * IntPtr.Size, messagePointer);
			}

			// Call the conversation function.
			IntPtr responseArrayPointer = IntPtr.Zero;
			PamStatus responseStatus = conversationFunction(
				num_msg: validMessageCount,
				msg: messagesPointerArray,				// const struct pam_message **
				resp: out responseArrayPointer,			// struct pam_response **
				appdata_ptr: conversation.appdata_ptr
			);

			if ((responseStatus != PamStatus.PAM_SUCCESS) || (responseArrayPointer == IntPtr.Zero)) {
				throw new PamException(PamStatus.PAM_CONV_ERR, LogLevel.Error);
			}

			// Extract the responses (in the same order).
			Dictionary<String, String> responses = new Dictionary<String, String>();

			try {
				for (Int32 validMessageIndex = 0; validMessageIndex < validMessageCount; validMessageIndex++) {
					// Get the valid message at the same index, so we have the key for the result.
					PamUserPrompt validMessage = validMessages[validMessageIndex];

					// Get the pam_response at the same index from the respArrayPtr array.
					IntPtr responsePointer = IntPtr.Add(responseArrayPointer, validMessageIndex * Marshal.SizeOf<pam_response>());
					pam_response response = Marshal.PtrToStructure<pam_response>(responsePointer);

					if (response.resp != IntPtr.Zero) {
						responses.Add(validMessage.Key, Marshal.PtrToStringUTF8(response.resp) ?? String.Empty);

						// The conversation function allocated the string with malloc.
						Marshal.FreeCoTaskMem(response.resp);
						// Note: On Linux the correct free is usually libc free().
						// FreeCoTaskMem works on Windows; on Linux you may prefer:
						// NativeMemory.Free(resp.resp) or a P/Invoke to free().

						// TODO: Wipe it.
					}
				}
			} finally {
				// Free the array of pam_response structures.
				Marshal.FreeHGlobal(responseArrayPointer);
			}

			return responses;
		} finally {
			// Clean up the allocated messages.
			for (Int32 validMessageIndex = 0; validMessageIndex < validMessageCount; validMessageIndex++) {
				IntPtr messagePointer = IntPtr.Add(messagesPointer, validMessageIndex * Marshal.SizeOf<pam_message>());
				pam_message message = Marshal.PtrToStructure<pam_message>(messagePointer);
				if (message.msg != IntPtr.Zero) {
					Marshal.FreeCoTaskMem(message.msg);
				}
			}

			Marshal.FreeHGlobal(messagesPointer);
			Marshal.FreeHGlobal(messagesPointerArray);
		}
	} // PromptUser
	#endregion

} // PamProviderBase
