using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
namespace RpcScandinavia.Repository.PamNss.Internal;

/// <summary>
/// This can execute a process natively, which work in a forked process.
/// Written by AI.
/// </summary>
public static unsafe partial class PosixProcess {
	private const Int32 STDOUT_FILENO = 1;
	private const Int32 STDERR_FILENO = 2;

	private const Int32 WNOHANG = 1;
	private const Int32 SIGKILL = 9;

	private const Int32 EINTR = 4;
	private const Int32 ECHILD = 10;
	private const Int32 EAGAIN = 11;
	private const Int32 ESRCH = 3;

	// Linux x86-64/glibc target. See ABI note below.
	private const nuint SpawnOpaqueSize = 256;
	private const nuint SpawnOpaqueAlignment = 16;

	private const Int32 O_CLOEXEC = 0x80000;
	private const Int32 O_NONBLOCK = 0x800;
	private const Int16 POLLIN = 0x001;
	private const Int16 POLLHUP = 0x010;
	private const Int16 POLLERR = 0x008;
	private const Int32 POLL_TIMEOUT_MS = 20;

	private const Int32 MaxCapturedBytesPerStream = 16 * 1024 * 1024;

	[StructLayout(LayoutKind.Sequential)]
	private struct PollFd {
		public Int32 fd;
		public Int16 events;
		public Int16 revents;
	} // PollFd

	[LibraryImport("libc", SetLastError = true)]
	private static partial Int32 pipe2(Span<Int32> pipefd, Int32 flags);

	[LibraryImport("libc", SetLastError = true)]
	private static partial Int32 close(Int32 fd);

	[LibraryImport("libc", SetLastError = true)]
	private static unsafe partial nint read(Int32 fd, Byte* buf, nint count);

	[LibraryImport("libc", SetLastError = true)]
	private static partial Int32 kill(Int32 pid, Int32 sig);

	[LibraryImport("libc", SetLastError = true)]
	private static partial Int32 waitpid(Int32 pid, out Int32 status, Int32 options);

	[LibraryImport("libc", SetLastError = true)]
	private static partial Int32 poll(Span<PollFd> fds, nuint nfds, Int32 timeout);

	[LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8)]
	private static partial Int32 posix_spawnp(out Int32 pid, String file, ref Byte file_actions, IntPtr attrp, IntPtr argv, IntPtr envp);

	[LibraryImport("libc")]
	private static partial Int32 posix_spawn_file_actions_init(ref Byte file_actions);

	[LibraryImport("libc")]
	private static partial Int32 posix_spawn_file_actions_destroy(ref Byte file_actions);

	[LibraryImport("libc")]
	private static partial Int32 posix_spawn_file_actions_adddup2(ref Byte file_actions, Int32 fildes, Int32 newfildes);

	[LibraryImport("libc")]
	private static partial Int32 posix_spawn_file_actions_addclose(ref Byte file_actions, Int32 fildes);

	[LibraryImport("libc")]
	private static partial IntPtr strerror(Int32 errnum);

	/// <summary>
	/// Starts a program with "posix_spawnp", captures stdout and stderr, and waits for completion or timeout.
	///
	/// A null or empty environment dictionary inherits the current environment. A nonempty dictionary overrides inherited entries.
	///
	/// Throws an exception on spawn/wait/read errors or timeout.
	/// </summary>
	/// <param name="program">The program to execute.</param>
	/// <param name="arguments">The program arguments.</param>
	/// <param name="environment">The environment variables.</param>
	/// <param name="timeout">The timeout.</param>
	/// <param name="stdOut">The captured data from standard output.</param>
	/// <param name="stdErr">The captured data from standard error.</param>
	/// <returns>Returns the process exit code. If killed by a signal, returns 128 + signal.</returns>
	/// <exception cref="ArgumentException">The program is null or empty. The program or an argument contain a NUL character.</exception>
	/// <exception cref="ArgumentNullException">The arguments, or an argument is null.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The timeout is not set or <see cref="Timeout.InfiniteTimeSpan"/>.</exception>
	/// <exception cref="InvalidOperationException"></exception>
	/// <exception cref="OutOfMemoryException"></exception>
	/// <exception cref="TimeoutException">The timeout occured.</exception>
	public static Int32 PosixExecute(String program, String[] arguments, Dictionary<String, String> environment, TimeSpan timeout, out String stdOut, out String stdErr) {
		// Validate.
		ArgumentException.ThrowIfNullOrEmpty(program);
		ArgumentNullException.ThrowIfNull(arguments);

		if ((timeout < TimeSpan.Zero) && (timeout != Timeout.InfiniteTimeSpan)) {
			throw new ArgumentOutOfRangeException(nameof(timeout));
		}

		PosixProcess.ValidateCString(program, nameof(program));

		foreach (String argument in arguments) {
			ArgumentNullException.ThrowIfNull(argument);
			PosixProcess.ValidateCString(argument, nameof(arguments));
		}

		stdOut = String.Empty;
		stdErr = String.Empty;

		Int32 stdoutRead = -1;
		Int32 stdoutWrite = -1;
		Int32 stderrRead = -1;
		Int32 stderrWrite = -1;

		Byte* actions = null;
		Boolean actionsInitialized = false;
		Int32 pid = -1;
		Boolean childReaped = false;

		using MemoryStream stdoutBytes = new MemoryStream();
		using MemoryStream stderrBytes = new MemoryStream();

		try {
			Int32[] stdoutPipe = new Int32[2];
			Int32[] stderrPipe = new Int32[2];

			if (PosixProcess.pipe2(stdoutPipe, O_CLOEXEC | O_NONBLOCK) != 0) {
				throw PosixProcess.NativeError("pipe2(stdout)");
			}

			stdoutRead = stdoutPipe[0];
			stdoutWrite = stdoutPipe[1];

			if (PosixProcess.pipe2(stderrPipe, O_CLOEXEC | O_NONBLOCK) != 0) {
				throw PosixProcess.NativeError("pipe2(stderr)");
			}

			stderrRead = stderrPipe[0];
			stderrWrite = stderrPipe[1];

			// Allocate aligned storage for the opaque glibc structure.
			actions = (Byte*)NativeMemory.AlignedAlloc(SpawnOpaqueSize, SpawnOpaqueAlignment);

			if (actions == null) {
				throw new OutOfMemoryException("Unable to allocate posix_spawn file actions.");
			}

			NativeMemory.Clear(actions, SpawnOpaqueSize);

			if (PosixProcess.posix_spawn_file_actions_init(ref actions[0]) != 0) {
				throw new InvalidOperationException("posix_spawn_file_actions_init failed.");
			}

			actionsInitialized = true;

			PosixProcess.AddAction(PosixProcess.posix_spawn_file_actions_adddup2(ref actions[0], stdoutWrite, STDOUT_FILENO));
			PosixProcess.AddAction(PosixProcess.posix_spawn_file_actions_adddup2(ref actions[0], stderrWrite, STDERR_FILENO));
			PosixProcess.AddAction(PosixProcess.posix_spawn_file_actions_addclose(ref actions[0], stdoutRead));
			PosixProcess.AddAction(PosixProcess.posix_spawn_file_actions_addclose(ref actions[0], stderrRead));
			PosixProcess.AddAction(PosixProcess.posix_spawn_file_actions_addclose(ref actions[0], stdoutWrite));
			PosixProcess.AddAction(PosixProcess.posix_spawn_file_actions_addclose(ref actions[0], stderrWrite));

			List<String> argvStrings = new List<String>(arguments.Length + 1) {
				program
			};
			argvStrings.AddRange(arguments);

			using StringArrayBlock argv = new StringArrayBlock(argvStrings);
			using StringArrayBlock envp = PosixProcess.BuildEnvironment(environment);

			Int32 rc = PosixProcess.posix_spawnp(
				out pid,
				program,
				ref actions[0],
				IntPtr.Zero,
				argv.Pointer,
				envp.Pointer
			);

			// posix_spawn* returns the error number directly.
			if (rc != 0) {
				throw PosixProcess.NativeError("posix_spawnp", rc);
			}

			// Parent must close its write ends before waiting for EOF.
			PosixProcess.CloseOwned(ref stdoutWrite);
			PosixProcess.CloseOwned(ref stderrWrite);

			Stopwatch stopwatch = Stopwatch.StartNew();
			Boolean stdoutEof = false;
			Boolean stderrEof = false;
			Boolean processExited = false;
			Int32 exitCode = -1;

			Byte[] buffer = new Byte[8192];

			while ((processExited == false) || (stdoutEof == false) || (stderrEof == false)) {
				if (processExited == false) {
					Int32 waitResult = PosixProcess.TryWait(pid, out Int32 status);
					if (waitResult == pid) {
						processExited = true;
						childReaped = true;
						exitCode = PosixProcess.DecodeWaitStatus(status);
					}
				}

				if ((timeout != Timeout.InfiniteTimeSpan) && (stopwatch.Elapsed >= timeout)) {
					// Timeout covers both process execution and draining.
					if (childReaped == false) {
						PosixProcess.TerminateAndReap(pid);
						childReaped = true;
					}

					throw new TimeoutException($"Process {pid} exceeded timeout {timeout}.");
				}

				if ((stdoutEof == true) && (stderrEof == true)) {
					continue;
				}

				PollFd[] pollFds = {
					new PollFd {
						fd = (stdoutEof == true) ? -1 : stdoutRead,
						events = POLLIN
					},
					new PollFd
					{
						fd = (stderrEof == true) ? -1 : stderrRead,
						events = POLLIN
					}
				};

				Int32 pollResult = PosixProcess.poll(pollFds, (nuint)pollFds.Length, POLL_TIMEOUT_MS);
				if (pollResult < 0) {
					Int32 errno = Marshal.GetLastPInvokeError();
					if (errno == EINTR) {
						continue;
					}

					throw PosixProcess.NativeError("poll", errno);
				}

				if ((stdoutEof == false) && ((pollFds[0].revents & (POLLIN | POLLHUP | POLLERR)) != 0)) {
					stdoutEof = PosixProcess.DrainAvailable(stdoutRead, stdoutBytes, buffer);
				}

				if ((stderrEof == false) && ((pollFds[1].revents & (POLLIN | POLLHUP | POLLERR)) != 0)) {
					stderrEof = PosixProcess.DrainAvailable(stderrRead, stderrBytes, buffer);
				}

				if ((pollResult == 0) && (processExited == false) && (timeout == Timeout.InfiniteTimeSpan)) {
					// Continue polling; no deadline applies.
				}
			}

			stdOut = Encoding.UTF8.GetString(stdoutBytes.ToArray());
			stdErr = Encoding.UTF8.GetString(stderrBytes.ToArray());

			return exitCode;
		} finally {
			if ((pid > 0) && (childReaped == false)) {
				// Best-effort clean-up if an exception occurred after spawn.
				try {
					PosixProcess.TerminateAndReap(pid);
				} catch {
					// Preserve the original exception.
				}
			}

			PosixProcess.CloseOwned(ref stdoutRead);
			PosixProcess.CloseOwned(ref stdoutWrite);
			PosixProcess.CloseOwned(ref stderrRead);
			PosixProcess.CloseOwned(ref stderrWrite);

			if (actions != null) {
				if (actionsInitialized == true) {
					try {
						PosixProcess.posix_spawn_file_actions_destroy(ref actions[0]);
					} catch {
						// Preserve the original exception.
					}
				}

				NativeMemory.AlignedFree(actions);
			}
		}
	} // PosixExecute

	/// <summary>
	/// Reads available bytes from a file descriptor into a memory stream, respecting buffer limits and handling
	/// non-blocking I/O signals.
	/// </summary>
	/// <param name="fd">The file descriptor to read from.</param>
	/// <param name="destination">The memory stream destination for captured output.</param>
	/// <param name="buffer">The temporary byte buffer used for reading.</param>
	/// <returns>True if end-of-file (EOF) was reached; otherwise, false.</returns>
	/// <exception cref="InvalidOperationException">Thrown when a read system error occurs.</exception>
	private static Boolean DrainAvailable(Int32 fd, MemoryStream destination, Byte[] buffer) {
		while (true) {
			nint n;
			fixed (Byte* p = buffer) {
				n = PosixProcess.read(fd, p, buffer.Length);
			}

			if (n > 0) {
				Int32 count = (Int32)n;
				Int32 remaining = MaxCapturedBytesPerStream - (Int32)destination.Length;
				if (remaining > 0) {
					destination.Write(buffer, 0, Math.Min(count, remaining));
				}

				// Keep draining so the child cannot block on a full pipe.
				continue;
			}

			if (n == 0) {
				return true;			// EOF.
			}

			Int32 errno = Marshal.GetLastPInvokeError();
			if (errno == EINTR) {
				continue;
			}

			if (errno == EAGAIN) {
				return false;		// Would block.
			}

			throw PosixProcess.NativeError("read", errno);
		}
	} // DrainAvailable

	/// <summary>
	/// Attempts to non-blocking check the status of a child process using waitpid.
	/// </summary>
	/// <param name="pid">The process ID of the child.</param>
	/// <param name="status">The decoded process status output.</param>
	/// <returns>The process ID if it exited, 0 if it is still running, or throws on error.</returns>
	/// <exception cref="InvalidOperationException">Thrown when a waitpid system error occurs.</exception>
	private static Int32 TryWait(Int32 pid, out Int32 status) {
		while (true) {
			Int32 result = PosixProcess.waitpid(pid, out status, WNOHANG);
			if (result >= 0) {
				return result;
			}

			Int32 errno = Marshal.GetLastPInvokeError();
			if (errno == EINTR) {
				continue;
			}

			// ECHILD is an error, not a successful exit code.
			throw PosixProcess.NativeError("waitpid", errno);
		}
	} // TryWait

	/// <summary>
	/// Forcefully terminates a child process with SIGKILL and reaps its zombie state.
	/// </summary>
	/// <param name="pid">The process ID to terminate.</param>
	/// <exception cref="InvalidOperationException">Thrown if kill or waitpid fails.</exception>
	private static void TerminateAndReap(Int32 pid) {
		if (pid <= 0) {
			return;
		}

		if (PosixProcess.kill(pid, SIGKILL) != 0) {
			Int32 errno = Marshal.GetLastPInvokeError();

			// ESRCH means the process no longer exists.
			if (errno != ESRCH) {
				throw PosixProcess.NativeError("kill", errno);
			}
		}

		while (true) {
			Int32 result = PosixProcess.waitpid(pid, out _, 0);
			if (result == pid) {
				return;
			}

			if (result == -1) {
				Int32 errno = Marshal.GetLastPInvokeError();
				if (errno == EINTR) {
					continue;
				}

				if (errno == ECHILD) {
					throw PosixProcess.NativeError("waitpid: child already reaped", errno);
				}

				throw PosixProcess.NativeError("waitpid after kill", errno);
			}
		}
	} // TerminateAndReap

	/// <summary>
	/// Decodes the raw integer status returned by waitpid into an exit code or signal code.
	/// </summary>
	/// <param name="status">The raw status value from waitpid.</param>
	/// <returns>The process exit code, or 128 plus the termination signal number.</returns>
	/// <exception cref="InvalidOperationException">Thrown if the status format is unexpected.</exception>
	private static Int32 DecodeWaitStatus(Int32 status) {
		if ((status & 0x7f) == 0) {
			return (status >> 8) & 0xff;
		}

		Int32 signal = status & 0x7f;
		if ((signal != 0) && (signal != 0x7f)) {
			return 128 + signal;
		}

		throw new InvalidOperationException($"Unexpected wait status: 0x{status:x}");
	} // DecodeWaitStatus

	/// <summary>
	/// Validates the result code of a posix_spawn file action operation, throwing an exception on failure.
	/// </summary>
	/// <param name="result">The integer result code to check.</param>
	/// <exception cref="InvalidOperationException">Thrown if the result is non-zero.</exception>
	private static void AddAction(Int32 result) {
		if (result != 0) {
			throw PosixProcess.NativeError("posix_spawn file action", result);
		}
	} // AddAction

	/// <summary>
	/// Safely closes an owned file descriptor and invalidates its reference.
	/// </summary>
	/// <param name="fd">A reference to the file descriptor to close.</param>
	private static void CloseOwned(ref Int32 fd) {
		Int32 value = fd;
		fd = -1;

		if (value >= 0) {
			// Do not retry close() after EINTR: descriptor state can be platform-dependent and a retry could close a reused fd.
			PosixProcess.close(value);
		}
	} // CloseOwned

	/// <summary>
	/// Validate that the string value don't contain a NUL character.
	/// </summary>
	/// <param name="value">The string value.</param>
	/// <param name="parameter">The name of the argument, used in the exception message.</param>
	/// <exception cref="ArgumentException">When the string value contains a NUL character.</exception>
	private static void ValidateCString(String value, String parameter) {
		if (value.IndexOf('\0') >= 0) {
			throw new ArgumentException("Native arguments cannot contain NUL characters.", parameter);
		}
	} // ValidateCString

	/// <summary>
	/// Creates an InvalidOperationException populated with the system error code and description string via strerror.
	/// </summary>
	/// <param name="operation">The name of the failed native operation.</param>
	/// <param name="error">Optional explicit error code; defaults to the last P/Invoke error.</param>
	/// <returns>An initialized InvalidOperationException.</returns>
	private static InvalidOperationException NativeError(String operation, Int32? error = null) {
		Int32 code = error ?? Marshal.GetLastPInvokeError();
		IntPtr ptr = PosixProcess.strerror(code);
		String message = Marshal.PtrToStringUTF8(ptr) ?? $"errno {code}";

		return new InvalidOperationException($"{operation} failed: {code} ({message})");
	} // NativeError

	/// <summary>
	/// Builds an environment variable block merging current process environment with custom overrides.
	/// </summary>
	/// <param name="overrides">Optional dictionary of environment variable overrides.</param>
	/// <returns>A pinned string array block representing the environment block.</returns>
	/// <exception cref="ArgumentException">Thrown if an override key or value is invalid.</exception>
	private static StringArrayBlock BuildEnvironment(Dictionary<String, String> overrides) {
		Dictionary<String, String> values = new Dictionary<String, String>(StringComparer.Ordinal);

		foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables()) {
			values[(String)entry.Key] = entry.Value?.ToString() ?? String.Empty;
		}

		if (overrides is not null) {
			foreach (KeyValuePair<String, String> item in overrides) {
				ArgumentNullException.ThrowIfNull(item.Key);
				ArgumentNullException.ThrowIfNull(item.Value);

				if ((item.Key.Length == 0) || (item.Key.Contains('=') == true) || (item.Key.Contains('\0') == true)) {
					throw new ArgumentException($"Invalid environment variable name: '{item.Key}'.");
				}

				PosixProcess.ValidateCString(item.Value, nameof(overrides));
				values[item.Key] = item.Value;
			}
		}

		List<String> entries = new List<String>(values.Count);

		foreach (KeyValuePair<String, String> item in values) {
			entries.Add($"{item.Key}={item.Value}");
		}

		return new StringArrayBlock(entries);
	} // BuildEnvironment

	private sealed unsafe class StringArrayBlock : IDisposable {
		private readonly List<IntPtr> _strings = new();
		private readonly IntPtr[] _pointers;
		private GCHandle _handle;

		public IntPtr Pointer { get; }

		/// <summary>
		/// Initializes a new instance of the StringArrayBlock class, allocating and pinning native UTF-8 strings.
		/// </summary>
		/// <param name="strings">The list of managed strings to convert and pin.</param>
		/// <exception cref="ArgumentException">Thrown if any string contains a NUL character.</exception>
		public StringArrayBlock(IReadOnlyList<String> strings) {
			this._pointers = new IntPtr[strings.Count + 1];

			try {
				for (Int32 i = 0; i < strings.Count; i++) {
					PosixProcess.ValidateCString(strings[i], nameof(strings));

					Byte[] bytes = Encoding.UTF8.GetBytes(strings[i] + '\0');
					IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);

					try {
						Marshal.Copy(bytes, 0, ptr, bytes.Length);
					} catch {
						Marshal.FreeHGlobal(ptr);
						throw;
					}

					this._strings.Add(ptr);
					this._pointers[i] = ptr;
				}

				this._pointers[^1] = IntPtr.Zero;
				this._handle = GCHandle.Alloc(this._pointers, GCHandleType.Pinned);
				this.Pointer = this._handle.AddrOfPinnedObject();
			} catch {
				foreach (IntPtr ptr in this._strings) {
					Marshal.FreeHGlobal(ptr);
				}

				this._strings.Clear();
				if (this._handle.IsAllocated) {
					this._handle.Free();
				}

				throw;
			}
		} // StringArrayBlock

		/// <summary>
		/// Releases all native memory resources and unpins the pointer block.
		/// </summary>
		public void Dispose() {
			if (this._handle.IsAllocated) {
				this._handle.Free();
			}

			foreach (IntPtr ptr in this._strings) {
				Marshal.FreeHGlobal(ptr);
			}

			this._strings.Clear();
		} // Dispose

	} // StringArrayBlock

} // PosixProcess
