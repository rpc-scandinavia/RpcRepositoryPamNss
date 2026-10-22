using System;
using System.Runtime.InteropServices;
namespace RpcScandinavia.Repository.PamNss.Internal;

/// <summary>
/// Imports from PAM (libpam.so).
/// </summary>
public static partial class PamImports {

	#region PAM library import methods
	//------------------------------------------------------------------------------------------------------------------
	// PAM library import methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Get the username.
	/// Prefer this over pam_get_item(PAM_USER).
	///
	/// The pam_get_user function returns the name of the user specified by pam_start(3).
	/// If no user was specified it returns what pam_get_item (pamh, PAM_USER, ... ); would have returned.
	/// If this is NULL it obtains the username via the pam_conv(3) mechanism, it prompts the user with the first
	/// non-NULL string in the following list:
	///  * The prompt argument passed to the function.
	///  * What is returned by pam_get_item (pamh, PAM_USER_PROMPT, ... ).
	///  * The default prompt: "login: ".
	///
	/// See <see href="https://manpages.debian.org/testing/libpam-doc/pam_get_user.3.en.html"/>.
	/// </summary>
	/// <param name="pamh">The PAM handle representing the current user authentication session.</param>
	/// <param name="user">Pointer to the user name (result).</param>
	/// <param name="prompt">The optional prompt shown to the user, if PAM wasn't initialised with a username.</param>
	/// <returns>
	///  * PAM_SUCCESS: User name was successful retrieved.
	///  * PAM_SYSTEM_ERR: A NULL pointer was submitted.
	///  * PAM_CONV_ERR: The conversation method supplied by the application failed to obtain the username.
	///  * PAM_BUF_ERR: Memory buffer error.
	///  * PAM_ABORT: Error resuming an old conversation.
	///  * PAM_CONV_AGAIN: The conversation method supplied by the application is waiting for an event.
	/// </returns>
	[LibraryImport("libpam.so.0", StringMarshalling = StringMarshalling.Utf8)]
	public static partial PamStatus pam_get_user(IntPtr pamh, out IntPtr user, String prompt);

	/// <summary>
	/// Get a standard item.
	/// The pam_get_item function allows applications and PAM service modules to access and retrieve PAM information
	/// of item_type. Upon successful return, item contains a pointer to the value of the corresponding item.
	/// Note, this is a pointer to the actual data and should not be free()'ed or over-written!
	///
	/// See <see href="https://manpages.debian.org/testing/libpam-doc/pam_get_item.3.en.html"/>.
	/// </summary>
	/// <param name="pamh">The PAM handle representing the current user authentication session.</param>
	/// <param name="item_type">The item type to get.</param>
	/// <param name="item">Pointer to the item data (result).</param>
	/// <returns>
	///  * PAM_BAD_ITEM: The application attempted to set an undefined or inaccessible item.
	///  * PAM_BUF_ERR: Memory buffer error.
	///  * PAM_PERM_DENIED: The value of item was NULL.
	///  * PAM_SUCCESS: Data was successful updated.
	///  * PAM_SYSTEM_ERR: The pam_handle_t passed as first argument was invalid.
	/// </returns>
	[LibraryImport("libpam.so.0")]
	public static partial PamStatus pam_get_item(IntPtr pamh, PamItemType item_type, out IntPtr item);

	/// <summary>
	/// Set a standard item.
	/// The pam_set_item function allows applications and PAM service modules to access and to update PAM information
	/// of item_type. For this a copy of the object pointed to by the item argument is created.
	///
	/// See <see href="https://manpages.debian.org/testing/libpam-doc/pam_set_item.3.en.html"/>.
	/// </summary>
	/// <remarks>
	/// For all item_types, other than PAM_CONV and PAM_FAIL_DELAY, item is a pointer to a <NUL> terminated character
	/// string. In the case of PAM_CONV, item points to an initialized pam_conv structure. In the case of PAM_FAIL_DELAY,
	/// item is a function pointer: void (*delay_fn)(int retval, unsigned usec_delay, void *appdata_ptr)
	///
	/// Both, PAM_AUTHTOK and PAM_OLDAUTHTOK, will be reset before returning to the application.
	/// Which means an application is not able to access the authentication tokens.
	/// </remarks>
	/// <param name="pamh">The PAM handle representing the current user authentication session.</param>
	/// <param name="item_type">The item type to set.</param>
	/// <param name="item">Pointer to the item data. See the remarks.</param>
	/// <returns>
	///  * PAM_BAD_ITEM: The application attempted to set an undefined or inaccessible item.
	///  * PAM_BUF_ERR: Memory buffer error.
	///  * PAM_SUCCESS: Data was successful updated.
	///  * PAM_SYSTEM_ERR: The pam_handle_t passed as first argument was invalid.
	/// </returns>
	[LibraryImport("libpam.so.0")]
	public static partial PamStatus pam_set_item(IntPtr pamh, PamItemType item_type, IntPtr item);

	/// <summary>
	/// Attach private module data to the handle.
	/// The pam_set_data function associates a pointer to an object with the (hopefully) unique string module_data_name
	/// in the PAM context specified by the pamh argument.
	///
	/// PAM modules may be dynamically loadable objects. In general such files should not contain static variables.
	/// This function and its counterpart pam_get_data(3), provide a mechanism for a module to associate some data with
	/// the handle pamh. Typically a module will call the pam_set_data function to register some data under a (hopefully)
	/// unique module_data_name. The data is available for use by other modules too but not by an application.
	/// Since this functions stores only a pointer to the data, the module should not modify or free the content of it.
	///
	/// See <see href="https://manpages.debian.org/testing/libpam-doc/pam_set_data.3.en.html"/>.
	/// </summary>
	/// <remarks>
	/// The function cleanup() is associated with the data and, if non-NULL, it is called when this data is over-written
	/// or following a call to pam_end(3). The error_status argument to the cleanup function, is used to indicate to the
	/// module the sort of action it is to take in cleaning this data item. Values:
	///  * PAM_DATA_REPLACE: When a data item is being replaced (through a second call to pam_set_data) this mask is
	///                      used. Otherwise, the call is assumed to be from pam_end(3).
	///  * PAM_DATA_SILENT: Which indicates that the process would prefer to perform the cleanup() quietly.
	///                     That is, discourages logging/messages to the user. It is generally used to indicate that the
	///                     current closing of the library is in a fork(2)ed process, and that the parent will take care
	///                     of cleaning up things that exist outside of the current process space (files etc.).
	/// </remarks>
	/// <param name="pamh">The PAM handle representing the current user authentication session.</param>
	/// <param name="module_data_name">Unique name of the module data.</param>
	/// <param name="data">Pointer to the module data.</param>
	/// <param name="cleanup">The cleanup callback function, or null.</param>
	/// <returns>
	///  * PAM_BUF_ERR: Memory buffer error.
	///  * PAM_SUCCESS: Data was successfully stored.
	///  * PAM_SYSTEM_ERR: A NULL pointer was submitted as PAM handle or the function was called by an application.
	/// </returns>
	[LibraryImport("libpam.so.0", StringMarshalling = StringMarshalling.Utf8)]
	public static partial PamStatus pam_set_data(IntPtr pamh, String module_data_name, IntPtr data, IntPtr cleanup);

	/// <summary>
	/// Retrieve private module data previously stored with pam_set_data.
	/// This function together with the pam_set_data(3) function is useful to manage module-specific data meaningful
	/// only to the calling PAM module.
	///
	/// The pam_get_data function looks up the object associated with the (hopefully) unique string module_data_name in
	/// the PAM context specified by the pamh argument. A successful call to pam_get_data will result in data pointing
	/// to the object.
	/// Note, this data is not a copy and should be treated as constant by the module.
	///
	/// See <see href="https://manpages.debian.org/testing/libpam-doc/pam_get_data.3.en.html"/>.
	/// </summary>
	/// <param name="pamh">The PAM handle representing the current user authentication session.</param>
	/// <param name="module_data_name">Unique name of the module data.</param>
	/// <param name="data">Pointer to the module data.</param>
	/// <returns>
	///  * PAM_SUCCESS: Data was successful retrieved.
	///  * PAM_SYSTEM_ERR: A NULL pointer was submitted as PAM handle or the function was called by an application.
	///  * PAM_NO_MODULE_DATA: No module specific data is present.
	/// </returns>
	[LibraryImport("libpam.so.0", StringMarshalling = StringMarshalling.Utf8)]
	public static partial PamStatus pam_get_data(IntPtr pamh, String module_data_name, out IntPtr data);

	/// <summary>
	/// Write a message to the system logger.
	/// The pam_syslog function logs messages using syslog(3) and is intended for internal use by Linux-PAM and PAM
	/// service modules.
	///
	/// The priority argument is formed by ORing the facility and the level values as documented in the syslog(3) manual
	/// page.
	///
	/// The pam_vsyslog function performs the same task as pam_syslog() with the difference that it takes a set of
	/// arguments which have been obtained using the stdarg(3) variable argument list macros.
	///
	/// See <see href="https://manpages.debian.org/testing/libpam-doc/pam_syslog.3.en.html"/>.
	/// </summary>
	/// <param name="pamh">The PAM handle representing the current user authentication session.</param>
	/// <param name="priority">The logging level OR'ed with the logging facility.</param>
	/// <param name="fmt">The message</param>
	[LibraryImport("libpam.so.0", StringMarshalling = StringMarshalling.Utf8)]
	public static partial void pam_syslog(IntPtr pamh, Int32 priority, String fmt);

	/// <summary>
	/// Return a string describing a PAM error code.
	/// The pam_strerror function returns a pointer to a string describing the error code passed in the argument errnum,
	/// possibly using the LC_MESSAGES part of the current locale to select the appropriate language.
	/// This string must not be modified by the application. No library function will modify this string.
	///
	/// See <see href="https://manpages.debian.org/testing/libpam-doc/pam_strerror.3.en.html"/>.
	/// </summary>
	/// <param name="pamh">The PAM handle representing the current user authentication session.</param>
	/// <param name="errnum">The error number.</param>
	/// <returns>This function returns always a pointer to a string.</returns>
	[LibraryImport("libpam.so.0", StringMarshalling = StringMarshalling.Utf8)]
	public static partial IntPtr pam_strerror(IntPtr pamh, PamStatus errnum);
	#endregion

} // PamImports
