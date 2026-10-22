using System;
using System.Linq;
using RpcScandinavia.Repository.Proto;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// Extension methods that map gRPC data objects into module data transfer objects.
/// </summary>
public static class ClientDataMapperExtensions {

	extension (NssUserResponse userReply) {

		public NssUserDto ToUserDto() {
			return new NssUserDto(
				userReply.LinuxUserId,
				userReply.LinuxGroupId,
				userReply.LinuxUserName,
				userReply.LinuxUserFullName,
				userReply.LinuxUserName,
				userReply.LinuxUserShell,
				userReply.ExpiredAt.ToDateTimeOffset()
			);
		} // ToUserDto

// TODO: Implement server support for the return of specific shadow users.
		public NssShadowDto ToShadowDto() {
			return new NssShadowDto(
				userReply.LinuxUserName,
				"*",
				userReply.ExpiredAt.ToDateTimeOffset()
			);
		} // ToShadowDto

	} // NssUserResponse

	extension (NssGroupResponse groupReply) {

		public NssGroupDto ToGroupDto() {
			return new NssGroupDto(
				groupReply.LinuxGroupId,
				groupReply.LinuxGroupName,
				groupReply.LinuxGroupMembers.ToArray()
			);
		} // ToGroupDto

	} // NssGroupResponse

} // ClientDataMapperExtensions
