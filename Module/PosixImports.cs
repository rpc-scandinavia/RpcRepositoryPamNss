using System;
using System.Runtime.InteropServices;
namespace RpcScandinavia.Repository.PamNss.Internal;

/// <summary>
/// Imports.
/// </summary>
public static class PosixImports {

	/// <summary>
	/// Gets the unique identifier for the current process.
	/// The build in <see cref="System.Environment.ProcessId"/> return the parent process identifier, when called
	/// from a forked process (child).
	/// </summary>
	/// <returns>A number that represents the unique identifier for the current process.</returns>
	[DllImport("libc", EntryPoint = "getpid")]
	public static extern Int32 GetPid();

	/// <summary>
	/// Writes the message to the system logger.
	/// </summary>
	/// <param name="priority">The priority and logging level.</param>
	/// <param name="format">The string formatter. Use "s".</param>
	/// <param name="message">The message.</param>
	[DllImport("libc.so.6", EntryPoint = "syslog", CallingConvention = CallingConvention.Cdecl)]
	public static extern void Syslog(Int32 priority, String format, String message);

} // PosixImports
