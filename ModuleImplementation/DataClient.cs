using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.Logging;
using RpcScandinavia.Repository.Proto;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// The gRPC client perform requests and return data as module data transfer objects.
/// </summary>
public class DataClient : IDisposable {
	private readonly Configuration options;
	private readonly GrpcChannel grpcChannel;
	private readonly PamChannel.PamChannelClient pamChannel;
	private readonly NssChannel.NssChannelClient nssChannel;

	#region Constructors and IDisposable
	//------------------------------------------------------------------------------------------------------------------
	// Constructors and IDisposable.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Instantiates a new gRPC client.
	/// </summary>
	/// <param name="tenants">The tenants.</param>
	/// <param name="host">The RPC Repository server.</param>
	/// <param name="options">The configuration.</param>
	public DataClient(Configuration options) : this(null, options) {
	} // DataClient

	/// <summary>
	/// Instantiates a new gRPC client.
	/// </summary>
	/// <param name="logger">The optional logger.</param>
	/// <param name="options">The configuration.</param>
	public DataClient(ILogger logger, Configuration options) {
		this.options = options;

		// Validate.
		ArgumentNullException.ThrowIfNull(options);

		// Initialise gRPC.
		SocketsHttpHandler handler = new SocketsHttpHandler();
		handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
		handler.EnableMultipleHttp2Connections = false;
		handler.PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan;
		handler.ConnectTimeout = this.options.DataClientConnectTimeout;
		handler.KeepAlivePingDelay = this.options.DataClientKeepAlivePingDelay;
		handler.KeepAlivePingTimeout = this.options.DataClientKeepAlivePingTimeout;
		handler.KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always;

		MethodConfig methodConfig = new MethodConfig {
			Names = { MethodName.Default },
			RetryPolicy = new RetryPolicy {
				MaxAttempts = this.options.DataClientMaxAttempts,
				InitialBackoff = this.options.DataClientBackoffMilliseconds,
				MaxBackoff = this.options.DataClientBackoffMilliseconds,
				BackoffMultiplier = 1.0,
				RetryableStatusCodes = { StatusCode.Unavailable },
			},
		};
		ServiceConfig serviceConfig = new ServiceConfig {
			MethodConfigs = { methodConfig },
		};

		GrpcChannelOptions grpcOptions = new GrpcChannelOptions();
		grpcOptions.DisableResolverServiceConfig = true;
		grpcOptions.DisposeHttpClient = true;
		grpcOptions.LoggerFactory = (logger != null) ? new PrefixLogger(logger, "gRPC:") : null;	// Can be null.
		grpcOptions.HttpHandler = handler;
		grpcOptions.ServiceConfig = serviceConfig;
		grpcOptions.MaxReceiveMessageSize = 500 * 1024 * 1024;											// 500 MB.
		grpcOptions.MaxSendMessageSize = 500 * 1024 * 1024;												// 500 MB.

		this.grpcChannel = GrpcChannel.ForAddress(options.GetHostAndPort, grpcOptions);
		this.pamChannel = new PamChannel.PamChannelClient(grpcChannel);
		this.nssChannel = new NssChannel.NssChannelClient(grpcChannel);
	} // DataClient

	/// <summary>
	/// Disposes the gRPC channel and HTTP client.
	/// </summary>
	public void Dispose() {
		this.grpcChannel.Dispose();
	} // Dispose
	#endregion

	#region Properties
	//------------------------------------------------------------------------------------------------------------------
	// Properties.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Gets the call options.
	/// </summary>
	public CallOptions CallOptions {
		get {
			return new CallOptions(deadline: DateTime.UtcNow.AddMilliseconds(this.options.DataClientCallDeadlineMilliseconds));
		}
	} // CallOptions

	/// <summary>
	/// Gets the PAM gRPC client.
	/// </summary>
	public PamChannel.PamChannelClient PamClient {
		get {
			return this.pamChannel;
		}
	} // PamClient

	/// <summary>
	/// Gets the NSS gRPC client.
	/// </summary>
	public NssChannel.NssChannelClient NssClient {
		get {
			return this.NssClient;
		}
	} // NssClient
	#endregion

	#region PAM methods
	//------------------------------------------------------------------------------------------------------------------
	// PAM methods.
	//------------------------------------------------------------------------------------------------------------------
// TODO: Move gRPC usage into this class.
	#endregion

	#region NSS methods
	//------------------------------------------------------------------------------------------------------------------
	// NSS methods.
	//------------------------------------------------------------------------------------------------------------------
	public NssUserDto[] GetAllUsers() {
		// Query the server.
		using AsyncServerStreamingCall<NssUserResponse> grpcReply = this.nssChannel.GetAllUsers(
			new NssAllRequest {
				Tenants = { this.options.Tenants },
			},
			 this.CallOptions
		);

		// Return the users.
		return grpcReply.ResponseStream.ReadAllAsync()
			.Select((user) => user.ToUserDto())
			.ToArrayAsync()
			.ConfigureAwait(false)
			.GetAwaiter()
			.GetResult();
	} // GetAllUsers

	public NssUserDto GetUserById(UInt32 userId) {
		// Query the server.
		NssUserResponse grpcReply = this.nssChannel.GetUserByUid(
			new NssUidRequest {
				Tenants = { this.options.Tenants },
				LinuxUserId = userId,
			},
			 this.CallOptions
		);

		// Handle failure.
		if (grpcReply.Result == false) {
			throw new NssException(NssStatus.UNAVAIL, LogLevel.Error, $"Userid '{userId}'.");
		}

		// Return the user.
		return grpcReply.ToUserDto();
	} // GetUserById

	public NssUserDto GetUserByName(String userName) {
		// Query the server.
		NssUserResponse grpcReply = this.nssChannel.GetUserByName(
			new NssUserRequest {
				Tenants = { this.options.Tenants },
				LinuxUserName = userName,
			},
			 this.CallOptions
		);

		// Handle failure.
		if (grpcReply.Result == false) {
			throw new NssException(NssStatus.UNAVAIL, LogLevel.Error, $"Username '{userName}'.");
		}

		// Return the user.
		return grpcReply.ToUserDto();
	} // GetUserByName

	public UInt32[] GetUserMembershipsByName(String userName) {
		// Query the server.
		NssUserMembershipsResponse grpcReply = this.nssChannel.GetUserMembershipsByName(
			new NssUserMembershipRequest {
				Tenants = { this.options.Tenants },
				LinuxUserName = userName,
			},
			 this.CallOptions
		);

		// Handle failure.
		if (grpcReply.Result == false) {
			throw new NssException(NssStatus.UNAVAIL, LogLevel.Error, $"Username '{userName}'.");
		}

		// Return the group ids.
		return grpcReply.LinuxGroupIds.ToArray();
	} // GetUserMembershipsByName

	public NssShadowDto[] GetAllShadows() {
// TODO: Implement server support for the return of specific shadow users.
		// Query the server.
		using AsyncServerStreamingCall<NssUserResponse> grpcReply = this.nssChannel.GetAllUsers(
			new NssAllRequest {
				Tenants = { this.options.Tenants },
			},
			 this.CallOptions
		);

		// Return the shadows.
		return grpcReply.ResponseStream.ReadAllAsync()
			.Select((shadow) => shadow.ToShadowDto())
			.ToArrayAsync()
			.ConfigureAwait(false)
			.GetAwaiter()
			.GetResult();
	} // GetAllShadows

	public NssShadowDto GetShadowByName(String userName) {
// TODO: Implement server support for the return of specific shadow users.
		// Query the server.
		NssUserResponse grpcReply = this.nssChannel.GetUserByName(
			new NssUserRequest {
				Tenants = { this.options.Tenants },
				LinuxUserName = userName,
			},
			 this.CallOptions
		);

		// Handle failure.
		if (grpcReply.Result == false) {
			throw new NssException(NssStatus.UNAVAIL, LogLevel.Error, $"Username '{userName}'.");
		}

		// Return the user.
		return grpcReply.ToShadowDto();
	} // GetShadowByName

	public NssGroupDto[] GetAllGroups() {
		// Query the server.
		using AsyncServerStreamingCall<NssGroupResponse> grpcReply = this.nssChannel.GetAllGroups(
			new NssAllRequest {
				Tenants = { this.options.Tenants },
			},
			 this.CallOptions
		);

		// Return the groups.
		return grpcReply.ResponseStream.ReadAllAsync()
			.Select((group) => group.ToGroupDto())
			.ToArrayAsync()
			.ConfigureAwait(false)
			.GetAwaiter()
			.GetResult();
	} // GetAllGroups

	public NssGroupDto GetGroupById(UInt32 groupId) {
		// Query the server.
		NssGroupResponse grpcReply = this.nssChannel.GetGroupByGid(
			new NssGidRequest {
				Tenants = { this.options.Tenants },
				LinuxGroupId = groupId,
			},
			 this.CallOptions
		);

		// Handle failure.
		if (grpcReply.Result == false) {
			throw new NssException(NssStatus.UNAVAIL, LogLevel.Error, $"Group id '{groupId}'.");
		}

		// Return the group.
		return grpcReply.ToGroupDto();
	} // GetGroupById

	public NssGroupDto GetGroupByName(String groupName) {
		// Query the server.
		NssGroupResponse grpcReply = this.nssChannel.GetGroupByName(
			new NssGroupRequest {
				Tenants = { this.options.Tenants },
				LinuxGroupName = groupName,
			},
			 this.CallOptions
		);

		// Handle failure.
		if (grpcReply.Result == false) {
			throw new NssException(NssStatus.UNAVAIL, LogLevel.Error, $"Group name '{groupName}'.");
		}

		// Return the group.
		return grpcReply.ToGroupDto();
	} // GetGroupByName
	#endregion

} // DataClient
