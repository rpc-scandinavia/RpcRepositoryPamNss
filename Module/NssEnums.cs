using System;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// NSS status codes.
/// </summary>
public enum NssStatus {
	/// <summary>
	/// The service is temporarily unavailable; try again later.
	/// </summary>
	TRYAGAIN = -2,

	/// <summary>
	/// The data source is unavailable or unreachable.
	/// </summary>
	UNAVAIL = -1,

	/// <summary>
	/// The requested entry was not found in this data source.
	/// </summary>
	NOTFOUND = 0,

	/// <summary>
	/// The operation was successful.
	/// </summary>
	SUCCESS = 1,

	/// <summary>
	/// Do not search any further data sources.
	/// </summary>
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
