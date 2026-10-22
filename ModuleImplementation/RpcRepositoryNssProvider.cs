using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using RpcScandinavia.Repository.PamNss.Internal;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// Implements missing members that return users and groups from the RPC Repository using gRPC.
/// </summary>
/// <remarks>
/// DotNet HTTP communication don't work when the current process is forked, which happens regularly for NSS.
/// When the process if forked, the request is handled by the external "RpcRepositoryPamNssDataClient" program.
/// This is only used for NSS.
/// </remarks>
public class RpcRepositoryNssProvider : NssProviderBase, IDisposable {
	private readonly Configuration options;
	private readonly DataClient client;

	#region Constructors and IDisposable
	//------------------------------------------------------------------------------------------------------------------
	// Constructors and IDisposable.
	//------------------------------------------------------------------------------------------------------------------
	public RpcRepositoryNssProvider() : base() {
		// Get the configuration.
		this.options = new Configuration();
		this.logLevel = this.options.LogLevel;

		// Use gRPC data client in the process.
		if (this.processIsForked == false) {
			this.client = new DataClient(logger, this.options);

			// Log.
			if (this.logger.IsEnabled(LogLevel.Debug) == true) {
				this.logger.LogDebug($"This process is not forked, all gRPC calls will be performed in-process.");
			}
		} else {
			this.client = null;

			// Log.
			if (this.logger.IsEnabled(LogLevel.Debug) == true) {
				this.logger.LogDebug($"This process is forked, all gRPC calls will be performed through the gRPC data client.");
			}
		}
	} // RpcRepositoryNssProvider

	/// <inheritdoc/>
	public override void Dispose() {
		this.client?.Dispose();
		base.Dispose();
	} // Dispose
	#endregion

	#region Overridden methods
	//------------------------------------------------------------------------------------------------------------------
	// Overridden methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <inheritdoc/>
	protected override NssUserDto[] GetAllUsers() {
		if (this.client != null) {
			return this.client.GetAllUsers();
		} else {
			return this.Execute<NssUserDto[]>("GetAllUsers");
		}
	} // GetAllUsers

	/// <inheritdoc/>
	protected override NssUserDto GetUserById(UInt32 userId) {
		if (this.client != null) {
			return this.client.GetUserById(userId);
		} else {
			return this.Execute<NssUserDto>("GetUserById", userId.ToString());
		}
	} // GetUserById

	/// <inheritdoc/>
	protected override NssUserDto GetUserByName(String userName) {
		if (this.client != null) {
			return this.client.GetUserByName(userName);
		} else {
			return this.Execute<NssUserDto>("GetUserByName", userName);
		}
	} // GetUserByName

	/// <inheritdoc/>
	protected override UInt32[] GetUserMembershipsByName(String userName) {
		if (this.client != null) {
			return this.client.GetUserMembershipsByName(userName);
		} else {
			return this.Execute<UInt32[]>("GetUserMembershipsByName", userName);
		}
	} // GetUserMembershipsByName

	/// <inheritdoc/>
	protected override NssShadowDto[] GetAllShadows() {
		if (this.client != null) {
			return this.client.GetAllShadows();
		} else {
			return this.Execute<NssShadowDto[]>("GetAllShadows");
		}
	} // GetAllShadows

	/// <inheritdoc/>
	protected override NssShadowDto GetShadowByName(String userName) {
		if (this.client != null) {
			return this.client.GetShadowByName(userName);
		} else {
			return this.Execute<NssShadowDto>("GetShadowByName", userName);
		}
	} // GetShadowByName

	/// <inheritdoc/>
	protected override NssGroupDto[] GetAllGroups() {
		if (this.client != null) {
			return this.client.GetAllGroups();
		} else {
			return this.Execute<NssGroupDto[]>("GetAllGroups");
		}
	} // GetAllGroups

	/// <inheritdoc/>
	protected override NssGroupDto GetGroupById(UInt32 groupId) {
		if (this.client != null) {
			return this.client.GetGroupById(groupId);
		} else {
			return this.Execute<NssGroupDto>("GetGroupById", groupId.ToString());
		}
	} // GetGroupById

	/// <inheritdoc/>
	protected override NssGroupDto GetGroupByName(String groupName) {
		if (this.client != null) {
			return this.client.GetGroupByName(groupName);
		} else {
			return this.Execute<NssGroupDto>("GetGroupByName", groupName);
		}
	} // GetGroupByName
	#endregion

	#region Execute methods
	//------------------------------------------------------------------------------------------------------------------
	// Execute methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Gets whether the data client environment variable marker is set.
	/// </summary>
	private Boolean IsHelperProcess => (Environment.GetEnvironmentVariable(this.options.DataClientMarkerKey) == this.options.DataClientMarkerValue);

	private T Execute<T>(params String[] args) where T:class {
		// Validate.
		ArgumentNullException.ThrowIfNull(args);

		try {
			// Log.
			if (this.logger.IsEnabled(LogLevel.Debug) == true) {
				this.logger.LogDebug($"Executing the gRPC data client: '{this.options.DataClient.FullName}' {String.Join(" ", args)}");
			}

			// Do not recursively launch the data client.
			if (this.IsHelperProcess == true) {
				throw new NssException(NssStatus.UNAVAIL, LogLevel.Error, $"Executing the gRPC data client recursively is blocked!");
			}

			// Execute the external "RpcRepositoryPamNssDataClient" program.
			Dictionary<String, String> environment = new Dictionary<String, String>();
			environment.Add(this.options.DataClientMarkerKey, this.options.DataClientMarkerValue);
			Int32 exitCode = PosixProcess.PosixExecute(
				this.options.DataClient.FullName,
				args,
				environment,
				TimeSpan.FromSeconds(60),
				out String stdOut,
				out String stdErr
			);

			// Log.
			if (this.logger.IsEnabled(LogLevel.Debug) == true) {
				this.logger.LogDebug($"Executing Exit Code: '{exitCode}'");
				this.logger.LogDebug($"Executing StdOut: '{stdOut}'");
				this.logger.LogDebug($"Executing StdErr: '{stdErr}'");
			}

			// Handle failure.
			if (exitCode != 0) {
				throw new NssException(NssStatus.UNAVAIL, LogLevel.Error, $"Error received from gRPC data client.");
				//throw new NssException(NssStatus.UNAVAIL, LogLevel.Error, $"Error received from gRPC data client: {stdErr}");
			}

			// Deserialise and return the result.
			return DataSerializer.Default.Deserialize<T>(stdOut);
		} catch (NssException) {
			throw;
		} catch (Exception exception) {
			// Handle failure.
			throw new NssException(NssStatus.UNAVAIL, LogLevel.Error, $"Executing the gRPC data client error: {exception.Message}");
		}
	} // Execute
	#endregion

} // RpcRepositoryNssProvider
