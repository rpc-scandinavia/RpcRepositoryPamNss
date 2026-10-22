using System;
using Microsoft.Extensions.Logging;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// Logger that injects a prefix to every message that is logged.
/// </summary>
public class PrefixLogger : ILoggerFactory, ILogger {
	private readonly ILogger logger;
	private readonly String prefix;

	public PrefixLogger(ILogger logger, String prefix) {
		this.logger = logger;
		this.prefix = prefix ?? String.Empty;
	} // PrefixLogger

	public void Dispose() {
		// Do nothing.
	} // Dispose

	#region ILoggerFactory methods
	//------------------------------------------------------------------------------------------------------------------
	// ILoggerFactory methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Return itself.
	/// </summary>
	/// <param name="categoryName">The log category.</param>
	public ILogger CreateLogger(String categoryName) {
		return this;
	} // CreateLogger

	/// <summary>
	/// Does nothing.
	/// </summary>
	/// <param name="provider">The log provider.</param>
	public void AddProvider(ILoggerProvider provider) {
		// Do nothing.
	} // AddProvider
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

		// Wrap the formatter so it injects the prefix right before the message is rendered.
		Func<TState, Exception, String> prefixedFormatter = (formatterState, formatterException) => {
			String message = formatter(formatterState, formatterException);
			return $"{this.prefix} {message}".Replace("\n", $"\n{this.prefix} ");
		};

		// Log.
		this.logger.Log(logLevel, eventId, state, exception, prefixedFormatter);
	} // Log

	/// <inheritdoc/>
	public Boolean IsEnabled(LogLevel logLevel) {
		return this.logger.IsEnabled(logLevel);
	} // IsEnabled

	/// <inheritdoc/>
	public IDisposable BeginScope<TState>(TState state) where TState: notnull {
		return this.logger.BeginScope<TState>(state);
	} // BeginScope
	#endregion

} // PrefixLogger
