using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
namespace RpcScandinavia.Repository.PamNss.Internal;

/// <summary>
/// Base class for common code used by both the PAM provider base and the NSS provider base.
/// The following protected fields are available:
///
///  * logger
///  * logLevel
///  * logPrefix
///  * processId
///  * processIsForked
///
/// The following protected methods are available:
///
///  * RunProcess
///
/// This base class implements <see cref="Microsoft.Extensions.Logging.ILogger"/>, which writes to the system logger.
/// </summary>
public abstract class ProviderBase : IDisposable, ILogger {

	#region Constructors and Dispose methods
	//------------------------------------------------------------------------------------------------------------------
	// Constructors and Dispose methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Instantiates a new provider base class.
	/// </summary>
	protected ProviderBase() {
		this.logPrefix = String.Empty;
		this.logLevel = LogLevel.Debug;
		this.processId = PosixImports.GetPid();
		this.processIsForked = (this.processId != Environment.ProcessId);
	} // ProviderBase

	/// <inheritdoc/>
	public virtual void Dispose() {
	} // Dispose
	#endregion

	#region Protected fields.
	//------------------------------------------------------------------------------------------------------------------
	// Protected fields.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Gets or sets a prefix inserted in every logging.
	/// </summary>
	protected String logPrefix;

	/// <summary>
	/// Gets or sets the current logging level.
	/// </summary>
	protected LogLevel logLevel;

	/// <summary>
	/// Gets the logger.
	/// </summary>
	protected ILogger logger => this;

	/// <summary>
	/// Gets the unique identifier for the current process.
	/// </summary>
	protected readonly Int32 processId;

	/// <summary>
	/// Gets whether the current process is a fork.
	/// </summary>
	protected readonly Boolean processIsForked;
	#endregion

	#region Virtual methods
	//------------------------------------------------------------------------------------------------------------------
	// Virtual methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Gets or sets a ident inserted in every logging before the prefix.
	/// </summary>
	protected virtual String GetLogIdent() {
		return String.Empty;
	} // GetLogIdent
	#endregion

	#region Process methods.
	//------------------------------------------------------------------------------------------------------------------
	// Process methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Starts a program with "posix_spawnp", captures stdout and stderr, and waits for completion or timeout.
	/// A null or empty environment dictionary inherits the current environment. A nonempty dictionary overrides inherited entries.
	/// Throws an exception on spawn/wait/read errors or timeout.
	/// </summary>
	/// <param name="program">The program to execute.</param>
	/// <param name="arguments">The program arguments.</param>
	/// <param name="environment">The environment variables.</param>
	/// <param name="timeout">The timeout.</param>
	/// <param name="stdOut">The captured data from standard output.</param>
	/// <param name="stdErr">The captured data from standard error.</param>
	/// <returns>Returns the process exit code. If killed by a signal, returns 128 + signal.</returns>
	public Int32 RunProcess(String program, String[] arguments, Dictionary<String, String> environment, TimeSpan timeout, out String stdOut, out String stdErr) {
		return PosixProcess.PosixExecute(program, arguments, environment, timeout, out stdOut, out stdErr);
	} // RunProcess
	#endregion

	#region ILogger methods
	//------------------------------------------------------------------------------------------------------------------
	// ILogger methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <inheritdoc/>
	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, String> formatter) {
		// Validate.
		if (this.IsEnabled(logLevel) == false) {
			return;
		}
		if (formatter == null) {
			return;
		}

		// Format the message using the provided formatter.
		String message = formatter(state, exception);
		if ((String.IsNullOrEmpty(message) == true) && (exception == null)) {
			return;
		}

		// Append exception details if present.
		if (exception != null) {
			if (exception is NssException nssException) {
				if (String.IsNullOrWhiteSpace(nssException.Message) == false) {
					message = $"{message} ({nssException.Status}): {nssException.Message}";
				} else {
					message = $"{message} ({nssException.Status})";
				}
			} else {
				message = $"{message}: {exception.Message}";
			}
		}

		// Dispatch to your syslog wrapper.
		this.Syslog(logLevel, message);
	} // Log

	/// <inheritdoc/>
	public Boolean IsEnabled(LogLevel logLevel) {
		return (this.logLevel <= logLevel);
	} // IsEnabled

	/// <inheritdoc/>
	public IDisposable BeginScope<TState>(TState state) where TState: notnull {
		return null;
	} // BeginScope
	#endregion

	#region Syslog logging methods
	//------------------------------------------------------------------------------------------------------------------
	// Syslog logging methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Write a message to the system log (syslog).
	/// Multiple lines are logged individually.
	/// </summary>
	/// <param name="logLevel">The logging level.</param>
	/// <param name="message">The message.</param>
	private void Syslog(LogLevel logLevel, String message) {
		// Validate.
		if (logLevel == LogLevel.None) {
			return;
		}
		if (String.IsNullOrWhiteSpace(message) == true) {
			return;
		}

		// Get the priority.
		PosixSyslogLevel priority = logLevel switch {
			LogLevel.Trace => PosixSyslogLevel.LOG_NOTICE,
			LogLevel.Debug => PosixSyslogLevel.LOG_DEBUG,
			LogLevel.Information => PosixSyslogLevel.LOG_INFO,
			LogLevel.Warning => PosixSyslogLevel.LOG_WARNING,
			LogLevel.Error => PosixSyslogLevel.LOG_ERR,
			LogLevel.Critical => PosixSyslogLevel.LOG_CRIT,
			_ => PosixSyslogLevel.LOG_INFO,
		};

		// Write each line to the log.
		foreach (String line in message.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
			// Always use "%s" as the format string to prevent format-string vulnerabilities.
			String lineComplete = $"{this.GetLogIdent()} {this.logPrefix} {line}".Trim();
			PosixImports.Syslog((Int32)PosixSyslogFacility.LOG_AUTHPRIV | (Int32)priority, "%s", lineComplete);
		}
	} // Syslog
	#endregion

} // ProviderBase
