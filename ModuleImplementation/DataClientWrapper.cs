using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using RpcScandinavia.Repository.PamNss.Internal;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// The gRPC client wrapper perform requests and return data as module data transfer objects.
/// </summary>
/// <remarks>
/// DotNet HTTP communication don't work when the current process is forked, which happens regulary for NSS.
/// When the process if forked, the request is handled by the external "RpcRepositoryPamNssDataClient" program.
/// This is only used for NSS.
/// </remarks>
public class DataClientWrapper : IDisposable {
	private const String DataClientMarkerKey = "RPC_REPOSITORY_NSS_DATA_CLIENT";
	private const String DataClientMarkerValue = "1";
	private ILogger logger;
	private DataClient client;

	#region Constructors and IDisposable
	//------------------------------------------------------------------------------------------------------------------
	// Constructors and IDisposable.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Instantiates a new data client wrapper.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="processId">The unique identifier for the current process.</param>
	/// <param name="tenants">The tenants.</param>
	/// <param name="host">The RPC Repository server.</param>
	/// <param name="certificate">The TLS certificate used by the server.</param>
	public DataClientWrapper(ILogger logger, Boolean processIsForked, String[] tenants, Uri host, X509Certificate2 certificate) {
		// Use gRPC data client in the process.
		if (processIsForked == false) {
			this.logger = logger;
			this.client = new DataClient(logger, tenants, host, certificate, 3, 100);

			// Log.
			if (this.logger.IsEnabled(LogLevel.Debug) == true) {
				this.logger.LogDebug($"This process is not forked, all gRPC calls will be performed in-process.");
			}
		} else {
			this.logger = logger;
			this.client = null;

			// Log.
			if (this.logger.IsEnabled(LogLevel.Debug) == true) {
				this.logger.LogDebug($"This process is forked, all gRPC calls will be performed through the gRPC data client.");
			}
		}
	} // DataClientWrapper

	/// <summary>
	/// Disposes the gRPC data client.
	/// </summary>
	public void Dispose() {
		this.client?.Dispose();
	} // Dispose
	#endregion

	#region NSS methods
	//------------------------------------------------------------------------------------------------------------------
	// NSS methods.
	//------------------------------------------------------------------------------------------------------------------
	public NssUserDto[] GetAllUsers() {
		if (this.client != null) {
			return this.client.GetAllUsers();
		} else {
			return this.Execute<NssUserDto[]>("GetAllUsers");
		}
	} // GetAllUsers

	public NssUserDto GetUserById(UInt32 userId) {
		if (this.client != null) {
			return this.client.GetUserById(userId);
		} else {
			return this.Execute<NssUserDto>("GetUserById", userId.ToString());
		}
	} // GetUserById

	public NssUserDto GetUserByName(String userName) {
		if (this.client != null) {
			return this.client.GetUserByName(userName);
		} else {
			return this.Execute<NssUserDto>("GetUserByName", userName);
		}
	} // GetUserByName

	public UInt32[] GetUserMembershipsByName(String userName) {
		if (this.client != null) {
			return this.client.GetUserMembershipsByName(userName);
		} else {
			return this.Execute<UInt32[]>("GetUserMembershipsByName", userName);
		}
	} // GetUserMembershipsByName

	public NssShadowDto[] GetAllShadows() {
		if (this.client != null) {
			return this.client.GetAllShadows();
		} else {
			return this.Execute<NssShadowDto[]>("GetAllShadows");
		}
	} // GetAllShadows

	public NssShadowDto GetShadowByName(String userName) {
		if (this.client != null) {
			return this.client.GetShadowByName(userName);
		} else {
			return this.Execute<NssShadowDto>("GetShadowByName", userName);
		}
	} // GetShadowByName

	public NssGroupDto[] GetAllGroups() {
		if (this.client != null) {
			return this.client.GetAllGroups();
		} else {
			return this.Execute<NssGroupDto[]>("GetAllGroups");
		}
	} // GetAllGroups

	/// <inheritdoc/>
	public NssGroupDto GetGroupById(UInt32 groupId) {
		if (this.client != null) {
			return this.client.GetGroupById(groupId);
		} else {
			return this.Execute<NssGroupDto>("GetGroupById", groupId.ToString());
		}
	} // GetGroupById

	public NssGroupDto GetGroupByName(String groupName) {
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
	private Boolean IsHelperProcess => (Environment.GetEnvironmentVariable(DataClientWrapper.DataClientMarkerKey) == DataClientWrapper.DataClientMarkerValue);

	private T Execute<T>(params String[] args) where T:class {
		// Validate.
		ArgumentNullException.ThrowIfNull(args);

		try {
// TODO: Move to configuration.
			FileInfo program = new FileInfo("/usr/lib/rpc-scandinavia/rpc-repository/RpcRepositoryPamNssDataClient");

			// Log.
			if (this.logger.IsEnabled(LogLevel.Debug) == true) {
				this.logger.LogDebug($"Executing the gRPC data client: '{program.FullName}' {String.Join(" ", args)}");
			}

			// Do not recursively launch the data client.
			if (this.IsHelperProcess == true) {
				throw new NssException(NssStatus.UNAVAIL, LogLevel.Error, $"Executing the gRPC data client recursively is blocked!");
			}

			// Execute the external "RpcRepositoryPamNssDataClient" program.
			Dictionary<String, String> environment = new Dictionary<String, String>();
			environment.Add(DataClientWrapper.DataClientMarkerKey, DataClientWrapper.DataClientMarkerValue);
			Int32 exitCode = PosixProcess.PosixExecute(
				program.FullName,
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

} // DataClientWrapper
