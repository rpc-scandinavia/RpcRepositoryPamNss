using System;
using System.Collections.Generic;
using System.Linq;
namespace RpcScandinavia.Repository.PamNss.Test;

/// <summary>
/// Implements missing members that return users and groups from hardcoded test data.
/// </summary>
public class TestNssProvider : NssProviderBase, IDisposable {

	public TestNssProvider() : base() {
	} // TestNssProvider

	/// <inheritdoc/>
	public override void Dispose() {
		base.Dispose();
	} // Dispose

	protected override NssUserDto[] GetAllUsers() {
		return TestData.TestUsers;
	} // GetAllUsers

	/// <inheritdoc/>
	protected override NssUserDto GetUserById(UInt32 userId) {
		return TestData.TestUsers.SingleOrDefault((user) => (user.UserId == userId));
	} // GetUserById

	/// <inheritdoc/>
	protected override NssUserDto GetUserByName(String userName) {
		return TestData.TestUsers.SingleOrDefault((user) => (user.UserName == userName));
	} // GetUserByName

	/// <inheritdoc/>
	protected override UInt32[] GetUserMembershipsByName(String userName) {
		NssUserDto user = TestData.TestUsers.SingleOrDefault((user) => (user.UserName == userName));
		if (user == null) {
			return [];
		}

		HashSet<UInt32> groups = new HashSet<UInt32>();
		groups.Add(user.GroupId);
		foreach (NssGroupDto group in TestData.TestGroups.Where((group) => (group.Members.Contains(userName) == true)).ToArray()) {
			groups.Add(group.GroupId);
		}

		return groups.ToArray();
	} // GetUserMembershipsByName

	/// <inheritdoc/>
	protected override NssShadowDto[] GetAllShadows() {
		return TestData.TestShadows;
	} // GetAllShadows

	/// <inheritdoc/>
	protected override NssShadowDto GetShadowByName(String userName) {
		return TestData.TestShadows.SingleOrDefault((shadow) => (shadow.UserName == userName));
	} // GetShadowByName

	/// <inheritdoc/>
	protected override NssGroupDto[] GetAllGroups() {
		return TestData.TestGroups;
	} // GetAllGroups

	/// <inheritdoc/>
	protected override NssGroupDto GetGroupById(UInt32 groupId) {
		return TestData.TestGroups.SingleOrDefault((group) => (group.GroupId == groupId));
	} // GetGroupById

	/// <inheritdoc/>
	protected override NssGroupDto GetGroupByName(String groupName) {
		return TestData.TestGroups.SingleOrDefault((group) => (group.GroupName == groupName));
	} // GetGroupByName

} // TestNssProvider
