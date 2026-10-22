using System;
using Microsoft.Extensions.Logging;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// NSS exception.
/// </summary>
public class NssException : Exception {
	private LogLevel logLevel;
	private NssStatus status;

	/// <summary>
	/// Instantiates a new NSS exception with the specific status code.
	/// </summary>
	/// <param name="status">The status code.</param>
	/// <param name="logLevel">The logging level (Error).</param>
	public NssException(NssStatus status, LogLevel logLevel = LogLevel.Error) {
		this.logLevel = logLevel;
		this.status = status;
	} // NssException

	/// <summary>
	/// Instantiates a new NSS exception with the specific status code.
	/// </summary>
	/// <param name="status">The status code.</param>
	/// <param name="logLevel">The logging level.</param>
	/// <param name="message">The error message.</param>
	public NssException(NssStatus status, LogLevel logLevel, String message) : base(message ?? String.Empty) {
		this.logLevel = logLevel;
		this.status = status;
	} // NssException

	/// <summary>
	/// Instantiates a new NSS exception with the specific status code.
	/// </summary>
	/// <param name="status">The status code.</param>
	/// <param name="message">The error message.</param>
	public NssException(NssStatus status, String message) : base(message ?? String.Empty) {
		this.logLevel = LogLevel.Error;
		this.status = status;
	} // NssException

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
	public NssStatus Status {
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

} // NssException
