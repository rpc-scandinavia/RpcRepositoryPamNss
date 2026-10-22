using System;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// Implements missing members that return users and groups from the RPC Repository using gRPC.
/// </summary>
public class RpcRepositoryNssProvider : NssProviderBase, IDisposable {
	private readonly Configuration options;
	private readonly DataClientWrapper client;

	#region Constructors and IDisposable
	//------------------------------------------------------------------------------------------------------------------
	// Constructors and IDisposable.
	//------------------------------------------------------------------------------------------------------------------
	public RpcRepositoryNssProvider() : base() {
		// Get the configuration.
		this.options = new Configuration();
		this.logLevel = this.options.LogLevel;
		this.client = new DataClientWrapper(
			this.logger,
			this.processIsForked,
			[this.options.Tenant],
			this.options.GetHostAndPort,
			this.options.Certificate
		);
	} // RpcRepositoryNssProvider

	/// <inheritdoc/>
	public override void Dispose() {
		this.client.Dispose();
		base.Dispose();
	} // Dispose
	#endregion

	#region Overridden methods
	//------------------------------------------------------------------------------------------------------------------
	// Overridden methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <inheritdoc/>
	protected override NssUserDto[] GetAllUsers() {
		return this.client.GetAllUsers();
	} // GetAllUsers

	/// <inheritdoc/>
	protected override NssUserDto GetUserById(UInt32 userId) {
		return this.client.GetUserById(userId);
	} // GetUserById

	/// <inheritdoc/>
	protected override NssUserDto GetUserByName(String userName) {
		return this.client.GetUserByName(userName);
	} // GetUserByName

	/// <inheritdoc/>
	protected override UInt32[] GetUserMembershipsByName(String userName) {
		return this.client.GetUserMembershipsByName(userName);
	} // GetUserMembershipsByName

	/// <inheritdoc/>
	protected override NssShadowDto[] GetAllShadows() {
		return this.client.GetAllShadows();
	} // GetAllShadows

	/// <inheritdoc/>
	protected override NssShadowDto GetShadowByName(String userName) {
		return this.client.GetShadowByName(userName);
	} // GetShadowByName

	/// <inheritdoc/>
	protected override NssGroupDto[] GetAllGroups() {
		return this.client.GetAllGroups();
	} // GetAllGroups

	/// <inheritdoc/>
	protected override NssGroupDto GetGroupById(UInt32 groupId) {
		return this.client.GetGroupById(groupId);
	} // GetGroupById

	/// <inheritdoc/>
	protected override NssGroupDto GetGroupByName(String groupName) {
		return this.client.GetGroupByName(groupName);
	} // GetGroupByName
	#endregion

} // RpcRepositoryNssProvider
