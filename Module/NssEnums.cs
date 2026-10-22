using System;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// NSS status codes.
/// </summary>
public enum NssStatus {
	TRYAGAIN = -2,
	UNAVAIL = -1,
	NOTFOUND = 0,
	SUCCESS = 1,
	RETURN = 2,
} // NssStatus

/// <summary>
/// NSS error codes.
/// See <see href="https://www.chromium.org/chromium-os/developer-library/reference/linux-constants/errnos/"/>.
/// </summary>
public enum NssError {
	/// <summary>
	/// No error.
	/// </summary>
	None = 0,

	/// <summary>
	/// A signal was caught; see signal(7) (EINTR).
	/// </summary>
	CaughtSignal = 0x04,

    /// <summary>
    /// I/O error (EIO).
    /// </summary>
	InputOutput = 0x05,

    /// <summary>
    /// The per-process limit on the number of open file descriptors has been reached (EMFILE).
    /// </summary>
	TooManyOpenProcessFiles = 0x18,

    /// <summary>
    /// The system-wide limit on the total number of open files has been reached (ENFILE).
    /// </summary>
	TooManyOpenSystemFiles = 0x17,

    /// <summary>
    /// Insufficient memory to allocate passwd structure (ENOMEM).
    /// </summary>
	NoMemory = 0x0c,

    /// <summary>
    /// Insufficient buffer space supplied (ERANGE).
    /// </summary>
	BufferOutOfRange = 0x22,

} // NssError
