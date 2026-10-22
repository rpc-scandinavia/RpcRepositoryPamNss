using System;
using System.Runtime.InteropServices;
namespace RpcScandinavia.Repository.PamNss.Internal;

/// <summary>
/// The conversation channel.
/// This is used to prompt the user or display a message to the user.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct pam_conv {
	public IntPtr conv;						// PamConvFunc
	public IntPtr appdata_ptr;
} // pam_conv

/// <summary>
/// The conversation function.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate PamStatus PamConvFunc(
	Int32 num_msg,
	IntPtr msg,								// const struct pam_message **
	out IntPtr resp,						// struct pam_response **
	IntPtr appdata_ptr
); // PamConvFunc

/// <summary>
/// The prompt or message to show the user.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct pam_message {
	public PamMessageStyle msg_style;		// PAM_PROMPT_ECHO_OFF, PAM_PROMPT_ECHO_ON, …
	public IntPtr msg;						// const char *
} // pam_message

/// <summary>
/// The prompt response.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct pam_response {
	public IntPtr resp;						// char * (allocated by the conversation function).
	public Int32 resp_retcode;				// currently unused, should be 0.
} // pam_response

/// <summary>
/// The data cleanup callback function.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void PamDataCleanupFunc(IntPtr pamh, IntPtr valuePointer, PamDataErrorStatus error_status);
