using System;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// User data transport between the NSS provider base class and the derived NSS implementation class.
/// </summary>
public class NssUserDto {
	public UInt32 UserId;
	public UInt32 GroupId;
	public String UserName;
	public String FullName;
	public String Home;
	public String Shell;
	public DateTimeOffset ExpiredAt;

	public NssUserDto(UInt32 userId, UInt32 groupId, String userName, String fullName, String home, String shell, DateTimeOffset expiredAt) {
		this.UserId = userId;
		this.GroupId = groupId;
		this.UserName = userName;
		this.FullName = fullName;
		this.Home = $"/home/rpc@rpc-scandinavia.dk"; //home;
		this.Shell = $"/bin/zsh"; //shell;
		this.ExpiredAt = expiredAt;
	} // NssUserDto

} // NssUserDto

/// <summary>
/// Shadow data transport between the NSS provider base class and the derived NSS implementation class.
/// </summary>
public class NssShadowDto {
	public String UserName;
	public String PasswordHash;
    public DateTimeOffset LastPasswordChange;
    public Int64 MinimumDaysBetweenPasswordChange;
    public Int64 MaximumDaysBetweenPasswordChange;
    public Int64 WarnDaysBeforeExpiration;
    public Int64 InactiveDaysAfterExpiration;
	public DateTimeOffset ExpiredAt;
    public UInt64 Flags = 0;

	public NssShadowDto(String userName, String passwordHash, DateTimeOffset expiredAt) {
		this.UserName = userName;
		this.PasswordHash = passwordHash ?? "*";
		this.LastPasswordChange = LastPasswordChange = new DateTimeOffset(new DateTime(2000, 01, 01));
		this.MinimumDaysBetweenPasswordChange = MinimumDaysBetweenPasswordChange = 0;
		this.MaximumDaysBetweenPasswordChange = MaximumDaysBetweenPasswordChange = 99999;
		this.WarnDaysBeforeExpiration = WarnDaysBeforeExpiration = 7;
		this.InactiveDaysAfterExpiration = InactiveDaysAfterExpiration = -1;
		this.ExpiredAt = expiredAt;
	} // NssShadowDto

	public NssShadowDto(
		String userName,
		String passwordHash,
		DateTimeOffset lastPasswordChange,
		Int64 minimumDaysBetweenPasswordChange,
		Int64 maximumDaysBetweenPasswordChange,
		Int64 warnDaysBeforeExpiration,
		Int64 inactiveDaysAfterExpiration,
		DateTimeOffset expiredAt
	) {
		this.UserName = userName;
		this.PasswordHash = passwordHash ?? "*";
		this.LastPasswordChange = lastPasswordChange;
		this.MinimumDaysBetweenPasswordChange = minimumDaysBetweenPasswordChange;
		this.MaximumDaysBetweenPasswordChange = maximumDaysBetweenPasswordChange;
		this.WarnDaysBeforeExpiration = warnDaysBeforeExpiration;
		this.InactiveDaysAfterExpiration = inactiveDaysAfterExpiration;
		this.ExpiredAt = expiredAt;
	} // NssShadowDto

} // NssShadowDto

/// <summary>
/// Group data transport between the NSS provider base class and the derived NSS implementation class.
/// </summary>
public class NssGroupDto {
	public UInt32 GroupId;
	public String GroupName;
	public String[] Members;

	public NssGroupDto(UInt32 groupId, String groupName, String[] members = null) {
		this.GroupId = groupId;
		this.GroupName = groupName;
		this.Members = members ?? [];
	} // NssGroupDto

} // NssGroupDto
