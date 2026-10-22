using System;
namespace RpcScandinavia.Repository.PamNss.Internal;

/// <summary>
/// System log levels (from syslog.h).
/// </summary>
public enum PosixSyslogLevel {
    /// <summary>
    /// System is unusable.
    /// </summary>
    LOG_EMERG   = 0,

    /// <summary>
    /// Action must be taken immediately.
    /// </summary>
    LOG_ALERT   = 1,

    /// <summary>
    /// Critical conditions.
    /// </summary>
    LOG_CRIT    = 2,

    /// <summary>
    /// Error conditions.
    /// </summary>
    LOG_ERR     = 3,

    /// <summary>
    /// Warning conditions.
    /// </summary>
    LOG_WARNING = 4,

    /// <summary>
    /// Normal, but significant, condition.
    /// </summary>
    LOG_NOTICE  = 5,

    /// <summary>
    /// Informational message.
    /// </summary>
    LOG_INFO    = 6,

    /// <summary>
    /// Debug-level message.
    /// </summary>
    LOG_DEBUG   = 7,
} // PosixSyslogLevel

/// <summary>
/// System log facility (from syslog.h).
/// </summary>
public enum PosixSyslogFacility {
	/// <summary>
	/// Normal authenticatin and authorisation logging.
	/// </summary>
	LOG_AUTH = 4 << 3,			// = 32

	/// <summary>
	/// Restricted authenticatin and authorisation logging.
	/// The log is routed to a more restricted log file (usually "/var/log/auth.log" or "/var/log/secure" with tighter
	/// permissions), which is appropriate for authentication-related messages.
	/// </summary>
	LOG_AUTHPRIV = 10 << 3,		// = 80
} // PosixSyslogFacility
