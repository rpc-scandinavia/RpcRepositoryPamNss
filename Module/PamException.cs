using System;
using Microsoft.Extensions.Logging;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// PAM exception.
/// </summary>
public class PamException : Exception {
	private LogLevel logLevel;
	private PamStatus status;

	/// <summary>
	/// Instantiates a new PAM exception with the specific status code.
	/// </summary>
	/// <param name="status">The status code.</param>
	/// <param name="logLevel">The logging level (Error).</param>
	public PamException(PamStatus status, LogLevel logLevel = LogLevel.Error) {
		this.logLevel = logLevel;
		this.status = status;
	} // PamException

	/// <summary>
	/// Instantiates a new PAM exception with the specific status code.
	/// </summary>
	/// <param name="status">The status code.</param>
	/// <param name="logLevel">The logging level.</param>
	/// <param name="message">The error message.</param>
	public PamException(PamStatus status, LogLevel logLevel, String message) : base(message ?? String.Empty) {
		this.logLevel = logLevel;
		this.status = status;
	} // PamException

	/// <summary>
	/// Instantiates a new PAM exception with the specific status code.
	/// </summary>
	/// <param name="status">The status code.</param>
	/// <param name="message">The error message.</param>
	public PamException(PamStatus status, String message) : base(message ?? String.Empty) {
		this.logLevel = LogLevel.Error;
		this.status = status;
	} // PamException

	/// <summary>
	/// Gets the logging level.
	/// </summary>
	public LogLevel LogLevel {
		get {
			return this.logLevel;
		}
	} // LogLevel

	/// <summary>
	/// Gets the status code.
	/// </summary>
	public PamStatus Status {
		get {
			return this.status;
		}
	} // Status

	/// <summary>
	/// Gets the status code as an integer.
	/// </summary>
	public Int32 StatusAsInt32 {
		get {
			return (Int32)this.status;
		}
	} // StatusAsInt32

} // PamException
