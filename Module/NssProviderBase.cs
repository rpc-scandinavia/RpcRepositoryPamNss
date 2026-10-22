using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using RpcScandinavia.Repository.PamNss.Internal;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// Extend and implement missing members in this base class to provide data for the NSS exports.
/// The following protected fields are available:
///
///  * logger
///  * logLevel
///  * logPrefix
///  * processId
///  * processIsForked
///
/// The following protected methods are available:
///
///  * RunProcess
///
/// This base class implements <see cref="Microsoft.Extensions.Logging.ILogger"/>, which writes to the system logger.
///
/// Implement the virtual methods and return the expected result, or throw a <see cref="NssException"/>.
/// The exception is caught and logged before the status is returned.
/// </summary>
public class NssProviderBase : ProviderBase, IDisposable {
	private static Int32 userCacheIndex = -1;
	private static Int32 shadowCacheIndex = -1;
	private static Int32 groupCacheIndex = -1;
	private static readonly String[] Shells = [
		"/usr/bin/zsh",
		"/usr/bin/bash",
		"/usr/bin/sh",
		"/bin/zsh",
		"/bin/bash",
		"/bin/sh",
	];

	private readonly NssCache cache;
	private String funcName;

	#region Constructors and Dispose methods
	//------------------------------------------------------------------------------------------------------------------
	// Constructors and Dispose methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Instantiates a new NSS provider.
	/// </summary>
	public NssProviderBase() : base() {
		this.cache = new NssCache(this.logger);
		this.funcName = String.Empty;
	} // NssProviderBase

	/// <inheritdoc/>
	public override void Dispose() {
		base.Dispose();
	} // Dispose
	#endregion

	//==================================================================================================================
	// Methods called from the exports.
	//==================================================================================================================

	#region Password
	//------------------------------------------------------------------------------------------------------------------
	// Password.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// See <see cref="NssExports.nss_setpwent"/> for full details.
	/// </summary>
	public NssStatus nss_setpwent(Int32 stayopen) {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.SUCCESS, "setpwent", (out debugString) => {
			// Initialise the cache.
			NssProviderBase.userCacheIndex = -1;

			// Return.
			debugString = $"Reset users iterator.";
			return NssStatus.SUCCESS;
		});
	} // nss_setpwent

	/// <summary>
	/// See <see cref="NssExports.nss_endpwent"/> for full details.
	/// </summary>
	public NssStatus nss_endpwent() {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.SUCCESS, "endpwent", (out debugString) => {
			// Destroy the cache.
			NssProviderBase.userCacheIndex = -1;

			// Return.
			debugString = $"Reset users iterator.";
			return NssStatus.SUCCESS;
		});
	} // nss_endpwent

	/// <summary>
	/// See <see cref="NssExports.nss_getpwent_r"/> for full details.
	/// </summary>
	public NssStatus nss_getpwent_r(IntPtr resultStructPtr, IntPtr bufferPtr, nuint bufferLengthValue, IntPtr errorNumberPtr) {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.NOTFOUND, "getpwent_r", (out debugString) => {
			// Get the users, either from the cache or from the virtual method.
			String cacheKey = $"users";
			NssUserDto[] users = this.cache.GetOrCreate<NssUserDto[]>(cacheKey, out Boolean fromCache, () => {
				return this.GetAllUsers();
			});

			// Validate.
			if (NssProviderBase.userCacheIndex + 1 >= users.Length) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Debug, $"User cache index {NssProviderBase.userCacheIndex + 1} out of range {users.Length}.");
			}

			// Get the user.
			NssProviderBase.userCacheIndex++;
			NssUserDto user = users[NssProviderBase.userCacheIndex];

			// Pack the user.
			user.Shell = this.ValidateShell(user.Shell);
			NssError error = this.PackPasswd(user, resultStructPtr, bufferPtr, bufferLengthValue);

			if (errorNumberPtr != IntPtr.Zero) {
				Marshal.WriteInt32(errorNumberPtr, (Int32)error);
			}

			if (error == NssError.BufferOutOfRange) {
				debugString = null;
				throw new NssException(NssStatus.TRYAGAIN, LogLevel.Debug, $"Buffer length '{bufferLengthValue}'.");
			}

			if (error != NssError.None) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Error, $"User packing error '{error}'.");
			}

			// Return.
			String fromCacheString = (fromCache == true) ? " from cache" : String.Empty;
			debugString = $"Returned '{user.UserName}' user{fromCacheString}, {NssProviderBase.userCacheIndex} of {users.Length}.";
			return NssStatus.SUCCESS;
		});
	} // nss_getpwent_r

	/// <summary>
	/// See <see cref="NssExports.nss_getpwnam_r"/> for full details.
	/// </summary>
	public NssStatus nss_getpwnam_r(IntPtr userNameStringPtr, IntPtr resultStructPtr, IntPtr bufferPtr, nuint bufferLengthValue, IntPtr errorNumberPtr) {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.NOTFOUND, "getpwnam_r", (out debugString) => {
			// Validate.
			if (userNameStringPtr == IntPtr.Zero) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Error, $"User is unspecified.");
			}

			// Unpack the username.
			String userName = Marshal.PtrToStringUTF8(userNameStringPtr);

			// Get the user, either from the cache or from the virtual method.
			String cacheKey = $"user_name_{userName}";
			NssUserDto user = this.cache.GetOrCreate<NssUserDto>(cacheKey, out Boolean fromCache, () => {
				return this.GetUserByName(userName);
			});

			if (user == null) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Information, $"User not found '{userName}'.");
			}

			// Pack the user.
			user.Shell = this.ValidateShell(user.Shell);
			NssError error = this.PackPasswd(user, resultStructPtr, bufferPtr, bufferLengthValue);

			if (errorNumberPtr != IntPtr.Zero) {
				Marshal.WriteInt32(errorNumberPtr, (Int32)error);
			}

			if (error == NssError.BufferOutOfRange) {
				debugString = null;
				throw new NssException(NssStatus.TRYAGAIN, LogLevel.Debug, $"Buffer length '{bufferLengthValue}'.");
			}

			if (error != NssError.None) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Error, $"User packing error '{error}'.");
			}

			// Return.
			String fromCacheString = (fromCache == true) ? " from cache" : String.Empty;
			debugString = $"Returned '{user.UserName}' user{fromCacheString}.";
			return NssStatus.SUCCESS;
		});
	} // nss_getpwnam_r

	/// <summary>
	/// See <see cref="NssExports.nss_getpwuid_r"/> for full details.
	/// </summary>
	public NssStatus nss_getpwuid_r(UInt32 uid, IntPtr resultStructPtr, IntPtr bufferPtr, nuint bufferLengthValue, IntPtr errorNumberPtr) {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.NOTFOUND, "getpwuid_r", (out debugString) => {
			// Get the user, either from the cache or from the virtual method.
			String cacheKey = $"user_id_{uid}";
			NssUserDto user = this.cache.GetOrCreate<NssUserDto>(cacheKey, out Boolean fromCache, () => {
				return this.GetUserById(uid);
			});

			if (user == null) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Information, $"User not found '{uid}'.");
			}

			// Pack the user.
			user.Shell = this.ValidateShell(user.Shell);
			NssError error = this.PackPasswd(user, resultStructPtr, bufferPtr, bufferLengthValue);

			if (errorNumberPtr != IntPtr.Zero) {
				Marshal.WriteInt32(errorNumberPtr, (Int32)error);
			}

			if (error == NssError.BufferOutOfRange) {
				debugString = null;
				throw new NssException(NssStatus.TRYAGAIN, LogLevel.Debug, $"Buffer length '{bufferLengthValue}'.");
			}

			if (error != NssError.None) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Error, $"User packing error '{error}'.");
			}

			// Return.
			String fromCacheString = (fromCache == true) ? " from cache" : String.Empty;
			debugString = $"Returned '{user.UserName}' user{fromCacheString}.";
			return NssStatus.SUCCESS;
		});
	} // nss_getpwuid_r
	#endregion

	#region Shadow
	//------------------------------------------------------------------------------------------------------------------
	// Shadow.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// See <see cref="NssExports.nss_setspent"/> for full details.
	/// </summary>
	public NssStatus nss_setspent(Int32 stayopen) {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.SUCCESS, "setspent", (out debugString) => {
			// Initialise the cache.
			NssProviderBase.shadowCacheIndex = -1;

			// Return.
			debugString = $"Reset shadows iterator.";
			return NssStatus.SUCCESS;
		});
	} // nss_setspent

	/// <summary>
	/// See <see cref="NssExports.nss_endspent"/> for full details.
	/// </summary>
	public NssStatus nss_endspent() {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.SUCCESS, "endspent", (out debugString) => {
			// Destroy the cache.
			NssProviderBase.shadowCacheIndex = -1;

			// Return.
			debugString = $"Reset shadows iterator.";
			return NssStatus.SUCCESS;
		});
	} // nss_endspent

	/// <summary>
	/// See <see cref="NssExports.nss_getspent_r"/> for full details.
	/// </summary>
	public NssStatus nss_getspent_r(IntPtr resultStructPtr, IntPtr bufferPtr, nuint bufferLengthValue, IntPtr errorNumberPtr) {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.NOTFOUND, "getspent_r", (out debugString) => {
			// Get the shadows, either from the cache or from the virtual method.
			String cacheKey = $"shadows";
			NssShadowDto[] shadows = this.cache.GetOrCreate<NssShadowDto[]>(cacheKey, out Boolean fromCache, () => {
				return this.GetAllShadows();
			});

			// Validate.
			if (NssProviderBase.shadowCacheIndex + 1 >= shadows.Length) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Debug, $"Shadow cache index {NssProviderBase.shadowCacheIndex + 1} out of range {shadows.Length}.");
			}

			// Get the shadow.
			NssProviderBase.shadowCacheIndex++;
			NssShadowDto shadow = shadows[NssProviderBase.shadowCacheIndex];

			// Pack the shadow.
			NssError error = this.PackShadow(shadow, resultStructPtr, bufferPtr, bufferLengthValue);

			if (errorNumberPtr != IntPtr.Zero) {
				Marshal.WriteInt32(errorNumberPtr, (Int32)error);
			}

			if (error == NssError.BufferOutOfRange) {
				debugString = null;
				throw new NssException(NssStatus.TRYAGAIN, LogLevel.Debug, $"Buffer length '{bufferLengthValue}'.");
			}

			if (error != NssError.None) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Error, $"Shadow packing error '{error}'.");
			}

			// Return.
			String fromCacheString = (fromCache == true) ? " from cache" : String.Empty;
			debugString = $"Returned '{shadow.UserName}' shadow{fromCacheString}, {NssProviderBase.shadowCacheIndex} of {shadows.Length}.";
			return NssStatus.SUCCESS;
		});
	} // nss_getspent_r

	/// <summary>
	/// See <see cref="NssExports.nss_getspnam_r"/> for full details.
	/// </summary>
	public NssStatus nss_getspnam_r(IntPtr userNameStringPtr, IntPtr resultStructPtr, IntPtr bufferPtr, nuint bufferLengthValue, IntPtr errorNumberPtr) {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.NOTFOUND, "getspnam_r", (out debugString) => {
			// Validate.
			if (userNameStringPtr == IntPtr.Zero) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Error, $"Shadow is unspecified.");
			}

			// Unpack the username.
			String userName = Marshal.PtrToStringUTF8(userNameStringPtr);

			// Get the shadow, either from the cache or from the virtual method.
			String cacheKey = $"shadow_name_{userName}";
			NssShadowDto shadow = this.cache.GetOrCreate<NssShadowDto>(cacheKey, out Boolean fromCache, () => {
				return this.GetShadowByName(userName);
			});

			if (shadow == null) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Information, $"Shadow not found '{userName}'.");
			}

			// Pack the shadow.
			NssError error = this.PackShadow(shadow, resultStructPtr, bufferPtr, bufferLengthValue);

			if (errorNumberPtr != IntPtr.Zero) {
				Marshal.WriteInt32(errorNumberPtr, (Int32)error);
			}

			if (error == NssError.BufferOutOfRange) {
				debugString = null;
				throw new NssException(NssStatus.TRYAGAIN, LogLevel.Debug, $"Buffer length '{bufferLengthValue}'.");
			}

			if (error != NssError.None) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Error, $"Shadow packing error '{error}'.");
			}

			// Return.
			String fromCacheString = (fromCache == true) ? " from cache" : String.Empty;
			debugString = $"Returned '{shadow.UserName}' shadow{fromCacheString}.";
			return NssStatus.SUCCESS;
		});
	} // nss_getspnam_r
	#endregion

	#region Group
	//------------------------------------------------------------------------------------------------------------------
	// Group.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// See <see cref="NssExports.nss_setgrent"/> for full details.
	/// </summary>
	public NssStatus nss_setgrent(Int32 stayopen) {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.SUCCESS, "setgrent", (out debugString) => {
			// Initialise the cache.
			NssProviderBase.groupCacheIndex = -1;

			// Return.
			debugString = $"Reset groups iterator.";
			return NssStatus.SUCCESS;
		});
	} // nss_setgrent

	/// <summary>
	/// See <see cref="NssExports.nss_endgrent"/> for full details.
	/// </summary>
	public NssStatus nss_endgrent() {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.SUCCESS, "endgrent", (out debugString) => {
			// Destroy the cache.
			NssProviderBase.groupCacheIndex = -1;

			// Return.
			debugString = $"Reset groups iterator.";
			return NssStatus.SUCCESS;
		});
	} // nss_endgrent

	/// <summary>
	/// See <see cref="NssExports.nss_getgrent_r"/> for full details.
	/// </summary>
	public NssStatus nss_getgrent_r(IntPtr resultStructPtr, IntPtr bufferPtr, nuint bufferLengthValue, IntPtr errorNumberPtr) {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.NOTFOUND, "getgrent_r", (out debugString) => {
			// Get the groups, either from the cache or from the virtual method.
			String cacheKey = $"groups";
			NssGroupDto[] groups = this.cache.GetOrCreate<NssGroupDto[]>(cacheKey, out Boolean fromCache, () => {
				return this.GetAllGroups();
			});

			// Validate.
			if (NssProviderBase.groupCacheIndex + 1 >= groups.Length) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Debug, $"Group cache index {NssProviderBase.groupCacheIndex + 1} out of range {groups.Length}.");
			}

			// Get the group.
			NssProviderBase.groupCacheIndex++;
			NssGroupDto group = groups[NssProviderBase.groupCacheIndex];

			// Pack the group.
			NssError error = this.PackGroup(group, resultStructPtr, bufferPtr, bufferLengthValue);

			if (errorNumberPtr != IntPtr.Zero) {
				Marshal.WriteInt32(errorNumberPtr, (Int32)error);
			}

			if (error == NssError.BufferOutOfRange) {
				debugString = null;
				throw new NssException(NssStatus.TRYAGAIN, LogLevel.Debug, $"Buffer length '{bufferLengthValue}'.");
			}

			if (error != NssError.None) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Error, $"Group packing error '{error}'.");
			}

			// Return.
			String fromCacheString = (fromCache == true) ? " from cache" : String.Empty;
			debugString = $"Returned '{group.GroupName}' group{fromCacheString}, {NssProviderBase.groupCacheIndex} of {groups.Length}.";
			return NssStatus.SUCCESS;
		});
	} // nss_getgrent_r

	/// <summary>
	/// See <see cref="NssExports.nss_getgrnam_r"/> for full details.
	/// </summary>
	public NssStatus nss_getgrnam_r(IntPtr groupNameStringPtr, IntPtr resultStructPtr, IntPtr bufferPtr, nuint bufferLengthValue, IntPtr errorNumberPtr) {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.NOTFOUND, "getgrnam_r", (out debugString) => {
			// Validate.
			if (groupNameStringPtr == IntPtr.Zero) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Error, $"Group is unspecified.");
			}

			// Unpack the group groupNameStringPtr.
			String groupName = Marshal.PtrToStringUTF8(groupNameStringPtr);

			// Get the group, either from the cache or from the virtual method.
			String cacheKey = $"group_name_{groupName}";
			NssGroupDto group = this.cache.GetOrCreate<NssGroupDto>(cacheKey, out Boolean fromCache, () => {
				return this.GetGroupByName(groupName);
			});

			if (group == null) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Information, $"Group not found '{groupName}'.");
			}

			// Pack the group.
			NssError error = this.PackGroup(group, resultStructPtr, bufferPtr, bufferLengthValue);

			if (errorNumberPtr != IntPtr.Zero) {
				Marshal.WriteInt32(errorNumberPtr, (Int32)error);
			}

			if (error == NssError.BufferOutOfRange) {
				debugString = null;
				throw new NssException(NssStatus.TRYAGAIN, LogLevel.Debug, $"Buffer length '{bufferLengthValue}'.");
			}

			if (error != NssError.None) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Error, $"Group packing error '{error}'.");
			}

			// Return.
			String fromCacheString = (fromCache == true) ? " from cache" : String.Empty;
			debugString = $"Returned '{group.GroupName}' group{fromCacheString}.";
			return NssStatus.SUCCESS;
		});
	} // nss_getgrnam_r

	/// <summary>
	/// See <see cref="NssExports.nss_getgrgid_r"/> for full details.
	/// </summary>
	public NssStatus nss_getgrgid_r(UInt32 gid, IntPtr resultStructPtr, IntPtr bufferPtr, nuint bufferLengthValue, IntPtr errorNumberPtr) {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.NOTFOUND, "getgrgid_r", (out debugString) => {
			// Get the group, either from the cache or from the virtual method.
			String cacheKey = $"group_id_{gid}";
			NssGroupDto group = this.cache.GetOrCreate<NssGroupDto>(cacheKey, out Boolean fromCache, () => {
				return this.GetGroupById(gid);
			});

			if (group == null) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Information, $"Group not found '{gid}'.");
			}

			// Pack the group.
			NssError error = this.PackGroup(group, resultStructPtr, bufferPtr, bufferLengthValue);

			if (errorNumberPtr != IntPtr.Zero) {
				Marshal.WriteInt32(errorNumberPtr, (Int32)error);
			}

			if (error == NssError.BufferOutOfRange) {
				debugString = null;
				throw new NssException(NssStatus.TRYAGAIN, LogLevel.Debug, $"Buffer length '{bufferLengthValue}'.");
			}

			if (error != NssError.None) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Error, $"Group packing error '{error}'.");
			}

			// Return.
			String fromCacheString = (fromCache == true) ? " from cache" : String.Empty;
			debugString = $"Returned '{group.GroupName}' group{fromCacheString}.";
			return NssStatus.SUCCESS;
		});
	} // nss_getgrgid_r
	#endregion

	#region InitGroups
	//------------------------------------------------------------------------------------------------------------------
	// InitGroups.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// See <see cref="NssExports.nss_initgroups_dyn"/> for full details.
	/// </summary>
	public NssStatus nss_initgroups_dyn(IntPtr userNameStringPtr, UInt32 group, IntPtr groupCountStartPtr, IntPtr groupBufferSizePtr, IntPtr groupsArrayPtr, Int64 limitValue, IntPtr errorNumberPtr) {
		// The wrapper handles exceptions and trace logging.
		return this.Wrap(NssStatus.NOTFOUND, "initgroups_dyn", (out debugString) => {
			// Validate.
			if (userNameStringPtr == IntPtr.Zero) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Error, $"User is unspecified.");
			}

			// Unpack the username.
			String userName = Marshal.PtrToStringUTF8(userNameStringPtr);

			// Redundant, provided by the group parameter.
			//// Get the user from the virtual method.
			//NssUserDto user = this.GetUserByName(username);
			//if (user == null) {
			//	throw new NssException(NssStatus.NOTFOUND, LogLevel.Information, $"'{username}'");
			//}

			// Get the memberships, either from the cache or from the virtual method.
			String cacheKey = $"memberships_name_{userName}";
			UInt32[] userMemberships = this.cache.GetOrCreate<UInt32[]>(cacheKey, out Boolean fromCache, () => {
				HashSet<UInt32> userMemberships = new HashSet<UInt32>();
				if (group < 65536) {
					userMemberships.Add(group);
				}
				foreach (UInt32 userMembership in this.GetUserMembershipsByName(userName)) {
					userMemberships.Add(userMembership);
				}

				return userMemberships.ToArray();
			});

			// Pack the user memberships.
			NssError error = this.PackMemberships(userMemberships, groupCountStartPtr, groupBufferSizePtr, groupsArrayPtr, limitValue);

			if (errorNumberPtr != IntPtr.Zero) {
				Marshal.WriteInt32(errorNumberPtr, (Int32)error);
			}

			if (error == NssError.BufferOutOfRange) {
				debugString = null;
				throw new NssException(NssStatus.TRYAGAIN, LogLevel.Debug, $"First buffer position: {Marshal.ReadInt64(groupCountStartPtr)}. Buffer length: {Marshal.ReadInt64(groupBufferSizePtr)}. Limit: {limitValue}.");
			}

			if (error != NssError.None) {
				throw new NssException(NssStatus.NOTFOUND, LogLevel.Error, $"Membership packing error '{error}'.");
			}

			// Return.
			String fromCacheString = (fromCache == true) ? " from cache" : String.Empty;
			debugString = $"Returned {userMemberships.Length} memberships for '{userName}' user{fromCacheString}{fromCacheString}.";
			return NssStatus.SUCCESS;
		});
	} // nss_initgroups_dyn
	#endregion

	//==================================================================================================================
	// Virtual methods.
	//==================================================================================================================

	#region Virtual methods
	//------------------------------------------------------------------------------------------------------------------
	// Virtual methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Get all the users.
	/// </summary>
	protected virtual NssUserDto[] GetAllUsers() {
		return [];
	} // GetAllUsers

	/// <summary>
	/// Get the user identified by the user id, or null.
	/// </summary>
	protected virtual NssUserDto GetUserById(UInt32 userId) {
		return null;
	} // GetUserById

	/// <summary>
	/// Get the user identified by the username, or null.
	/// </summary>
	protected virtual NssUserDto GetUserByName(String userName) {
		return null;
	} // GetUserByName

	/// <summary>
	/// Get the group id of all groups the specified user is memmber of.
	/// </summary>
	protected virtual UInt32[] GetUserMembershipsByName(String userName) {
		return [];
	} // GetUserMembershipsByName

	/// <summary>
	/// Get all the shadows.
	/// </summary>
	protected virtual NssShadowDto[] GetAllShadows() {
		return [];
	} // GetAllShadows

	/// <summary>
	/// Get the shadow identified by the username, or null.
	/// </summary>
	protected virtual NssShadowDto GetShadowByName(String userName) {
		return null;
	} // GetShadowByName

	/// <summary>
	/// Get all the groups.
	/// </summary>
	protected virtual NssGroupDto[] GetAllGroups() {
		return [];
	} // GetAllGroups

	/// <summary>
	/// Get the group identified by the group id, or null.
	/// </summary>
	protected virtual NssGroupDto GetGroupById(UInt32 groupId) {
		return null;
	} // GetGroupById

	/// <summary>
	/// Get the group identified by the group name, or null.
	/// </summary>
	protected virtual NssGroupDto GetGroupByName(String groupName) {
		return null;
	} // GetGroupByName
	#endregion

	#region Overridden methods
	//------------------------------------------------------------------------------------------------------------------
	// Overridden methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <inheritdoc/>
	protected override sealed String GetLogIdent() {
		return $"NSS [{this.funcName}]:";
	} // GetLogIdent
	#endregion

	//==================================================================================================================
	// Helper methods.
	//==================================================================================================================

	#region Wrap methods
	//------------------------------------------------------------------------------------------------------------------
	// Wrap methods.
	//------------------------------------------------------------------------------------------------------------------
	private delegate NssStatus WrapDelegate(out String debugString);

	/// <summary>
	/// Wrap the code in a try-catch block, and do the trace-debug logging.
	/// </summary>
	/// <param name="exceptionResult"></param>
	/// <param name="funcName"></param>
	/// <param name="func"></param>
	/// <returns></returns>
	private NssStatus Wrap(NssStatus exceptionResult, String funcName, WrapDelegate func) {
		try {
			// Get the function name, for the logger.
			this.funcName = funcName;

			// Log.
			if (this.logger.IsEnabled(LogLevel.Debug) == true) {
				this.logger.LogDebug($"Begin.");
			}

			// Call the function.
			String debugString = null;
			NssStatus funcReturn = func(out debugString);

			// Log.
			if (this.logger.IsEnabled(LogLevel.Debug) == true) {
				this.logger.LogDebug($"End => {funcReturn}: {debugString}");
			}

			// Return.
			return funcReturn;
		} catch (NssException exception) {
			// Log.
			if (this.logger.IsEnabled(exception.LogLevel) == true) {
				this.logger.Log(exception.LogLevel, $"End => {exception.Status}: {exception.Message}");
			}
			if (this.logger.IsEnabled(LogLevel.Trace) == true) {
				this.logger.LogDebug(exception.StackTrace);
			}

			// Return.
			return exception.Status;
		} catch (Exception exception) {
			// Log.
			if (this.logger.IsEnabled(LogLevel.Error) == true) {
				this.logger.LogError($"End => {exceptionResult}: {exception.Message}");
			}
			if (this.logger.IsEnabled(LogLevel.Trace) == true) {
				this.logger.LogDebug(exception.StackTrace);
			}

			// Return.
			return exceptionResult;
		}
	} // Wrap
	#endregion

	#region Validator methods
	//------------------------------------------------------------------------------------------------------------------
	// Validator methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Ensure that the shell program exist, or fallback to the first default found.
	/// </summary>
	/// <param name="shell">The desired shell.</param>
	/// <returns>The desired shell, or an alternative.</returns>
	private String ValidateShell(String shell) {
		if (File.Exists(shell) == true) {
			return shell;
		} else {
			foreach (String alternativeShell in NssProviderBase.Shells) {
				if (File.Exists(alternativeShell) == true) {
					return alternativeShell;
				}
			}

			return String.Empty;
		}
	} // ValidateShell
	#endregion

	#region Pack methods
	//------------------------------------------------------------------------------------------------------------------
	// Pack methods
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Pack the specified user into the <c>passwd</c> structure referenced by the pointer.
	/// </summary>
	/// <param name="user">The user.</param>
	/// <param name="resultStructPtr">Pointer to the <c>user</c> structure.</param>
	/// <param name="buffer">Pointer to the working memory buffer allocated by glibc.</param>
	/// <param name="bufferLengthValue">The size in bytes of the working buffer.</param>
	private unsafe NssError PackPasswd(NssUserDto user, IntPtr resultStructPtr, IntPtr bufferPtr, UIntPtr bufferLengthValue) {
		// Validate.
		if (user == null) {
			return NssError.NoMemory;													// or UNAVAIL.
		}

		// Prepare the strings (never null).
		String name = user.UserName ?? String.Empty;
		String passwd = "x";															// classic shadow placeholder.
		String gecos = user.FullName ?? String.Empty;
		String home = user.Home ?? String.Empty;
		String shell = user.Shell ?? String.Empty;

		// Calculate exact byte requirements (including the '\0').
		Int32 nameLen = Encoding.UTF8.GetByteCount(name) + 1;
		Int32 passLen = Encoding.UTF8.GetByteCount(passwd) + 1;
		Int32 gecosLen = Encoding.UTF8.GetByteCount(gecos) + 1;
		Int32 homeLen = Encoding.UTF8.GetByteCount(home) + 1;
		Int32 shellLen = Encoding.UTF8.GetByteCount(shell) + 1;

		Int32 totalBytes = nameLen + passLen + gecosLen + homeLen + shellLen;

		if ((UInt64)bufferLengthValue < (UInt64)totalBytes) {
			return NssError.BufferOutOfRange;
		}

		// Write the strings directly into the glibc buffer.
		Byte* buffer = (Byte*)bufferPtr;
		Byte* current = buffer;
		Byte* end = buffer + (Int64)bufferLengthValue;  // exclusive end

		// Helper that writes a UTF-8 string + NUL and advances 'current'.
		// Returns the pointer that should be stored in the struct.
		IntPtr WriteString(String text) {
			Int32 len = Encoding.UTF8.GetBytes(text ?? String.Empty, new Span<Byte>(current, (Int32)(end - current)));
			current[len] = 0;															// NUL terminator.
			IntPtr ptr = (IntPtr)current;
			current += len + 1;
			return ptr;
		} // WriteString

		// Fill the passwd structure.
		PasswdStruct resultStruct = new PasswdStruct {
			pw_name = WriteString(name),
			pw_passwd = WriteString(passwd),
			pw_uid = user.UserId,
			pw_gid = user.GroupId,
			pw_gecos = WriteString(gecos),
			pw_dir = WriteString(home),
			pw_shell = WriteString(shell)
		};

		// Final safety check (should never fire if the size math is correct).
		if (current > end) {
			return NssError.BufferOutOfRange;
		}

		Marshal.StructureToPtr(resultStruct, resultStructPtr, fDeleteOld: false);
		return NssError.None;
	} // PackPasswd

	/// <summary>
	/// Pack the specified shadow into the <c>shadow</c> (shadow) structure referenced by the pointer.
	/// </summary>
	/// <param name="shadowThe user containing username and password hash information.</param>
	/// <param name="resultStructPtr">Pointer to the <c>shadow</c> structure.</param>
	/// <param name="bufferPtr">Pointer to the working memory buffer allocated by glibc.</param>
	/// <param name="bufferLengthValue">The size in bytes of the working buffer.</param>
	private unsafe NssError PackShadow(NssShadowDto shadow, IntPtr resultStructPtr, IntPtr bufferPtr, UIntPtr bufferLengthValue) {
		// Validate.
		if (shadow == null) {
			return NssError.NoMemory;
		}

		// Prepare the two strings (never null).
// TODO: Get password hash from server, make it configurable. Falling back to a locked/disabled hash if empty.
// TODO: Account locked = "*" (or "!", "!!"). password must be changed on next login = "@".
		String name = shadow.UserName ?? String.Empty;
		String passwordHash = shadow.PasswordHash ?? "*";									// locked / disabled if missing.

		// Calculate exact byte requirements (including the '\0').
		Int32 nameLen = Encoding.UTF8.GetByteCount(name) + 1;
		Int32 passLen = Encoding.UTF8.GetByteCount(passwordHash) + 1;

		Int32 totalBytes = nameLen + passLen;

		if ((UInt64)bufferLengthValue < (UInt64)totalBytes)
			return NssError.BufferOutOfRange;

		// Write the strings directly into the glibc buffer.
		Byte* buffer = (Byte*)bufferPtr;
		Byte* current = buffer;
		Byte* end = buffer + (Int64)bufferLengthValue;

		// Helper that writes a UTF-8 string + NUL and advances 'current'.
		// Returns the pointer that should be stored in the struct.
		IntPtr WriteString(String text) {
			Int32 len = Encoding.UTF8.GetBytes(text ?? String.Empty, new Span<Byte>(current, (Int32)(end - current)));
			current[len] = 0;
			IntPtr ptr = (IntPtr)current;
			current += len + 1;
			return ptr;
		} // WriteString

		// Convert dates -> days since 1970-01-01 (or -1 = “not set”).
		static Int64 ToShadowDays(DateTimeOffset dt) {
			// Special value used by most NSS modules when the field is unset.
			if ((dt == default) || (dt == DateTimeOffset.MinValue)) {
				return -1;
			}

			return (Int64)(dt - DateTimeOffset.UnixEpoch).TotalDays;
		} // ToShadowDays

		Int64 lstchg = ToShadowDays(shadow.LastPasswordChange);
		Int64 expire = ToShadowDays(shadow.ExpiredAt);

		// Fill the spwd structure.
		ShadowStruct resultStruct = new ShadowStruct {
			sp_namp = WriteString(name),
			sp_pwdp = WriteString(passwordHash),
			sp_lstchg = lstchg,
			sp_min = shadow.MinimumDaysBetweenPasswordChange,
			sp_max = shadow.MaximumDaysBetweenPasswordChange,
			sp_warn = shadow.WarnDaysBeforeExpiration,
			sp_inact = shadow.InactiveDaysAfterExpiration,
			sp_expire = expire,
			sp_flag = shadow.Flags
		};

		// Final safety check.
		if (current > end) {
			return NssError.BufferOutOfRange;
		}

		Marshal.StructureToPtr(resultStruct, resultStructPtr, fDeleteOld: false);
		return NssError.None;
	} // PackShadow

	/// <summary>
	/// Pack the specified group into the <c>group</c> structure referenced by the pointer.
	/// </summary>
	/// <param name="group">The group.</param>
	/// <param name="resultStructPtr">Pointer to the <c>group</c> structure.</param>
	/// <param name="buffer">Pointer to the working memory buffer allocated by glibc.</param>
	/// <param name="bufferLengthValue">The size in bytes of the working buffer.</param>
	private unsafe NssError PackGroup(NssGroupDto group, IntPtr resultStructPtr, IntPtr bufferPtr, UIntPtr bufferLengthValue) {
		// Validate.
		if (group == null) {
			return NssError.NoMemory;
		}

		// Prepare values (never null).
		String groupName = group.GroupName ?? String.Empty;
		String passwd = "x";															// classic group placeholder.
		String[] members = group.Members ?? Array.Empty<String>();

		// Calculate exact byte requirements (including the '\0').
		Int32 nameLen = Encoding.UTF8.GetByteCount(groupName) + 1;
		Int32 passLen = Encoding.UTF8.GetByteCount(passwd) + 1;

		Int32 memberStringBytes = 0;
		foreach (String member in members) {
			memberStringBytes += Encoding.UTF8.GetByteCount(member ?? String.Empty) + 1;
		}

		// Pointer array + terminating NULL pointer.
		Int32 memberArrayBytes = (members.Length + 1) * IntPtr.Size;

		// Worst-case alignment padding (we may need up to IntPtr.Size - 1 bytes).
		Int32 maxPadding = IntPtr.Size - 1;

		Int32 totalBytes = nameLen + passLen + maxPadding + memberArrayBytes + memberStringBytes;

		if ((UInt64)bufferLengthValue < (UInt64)totalBytes) {
			return NssError.BufferOutOfRange;
		}

		// Write everything into the glibc buffer.
		Byte* buffer = (Byte*)bufferPtr;
		Byte* current = buffer;
		Byte* end = buffer + (Int64)bufferLengthValue;

		// Helper that writes a UTF-8 string + NUL and advances 'current'.
		// Returns the pointer that should be stored in the struct.
		IntPtr WriteString(String text) {
			Int32 len = Encoding.UTF8.GetBytes(text ?? String.Empty, new Span<Byte>(current, (Int32)(end - current)));
			current[len] = 0;															// NUL terminator.
			IntPtr ptr = (IntPtr)current;
			current += len + 1;
			return ptr;
		} // WriteString

		IntPtr grNamePtr = WriteString(groupName);
		IntPtr grPasswdPtr = WriteString(passwd);

		// Align 'current' for the pointer array (char**).
		nuint addr = (nuint)current;
		nuint align = (nuint)IntPtr.Size;
		nuint misalign = addr % align;
		if (misalign != 0) {
			current += (Int64)(align - misalign);
		}

		// Build the gr_mem array of char* inside the buffer.
		IntPtr* memPtrArray = (IntPtr*)current;
		current += memberArrayBytes;													// Reserve the pointer slots.

		for (Int32 index = 0; index < members.Length; index++) {
			memPtrArray[index] = WriteString(members[index]);
		}
		memPtrArray[members.Length] = IntPtr.Zero;										// NULL terminator required by POSIX.

		// Fill the group structure.
		GroupStruct resultStruct = new GroupStruct {
			gr_name = grNamePtr,
			gr_passwd = grPasswdPtr,
			gr_gid = group.GroupId,
			gr_mem = (IntPtr)memPtrArray
		};

		// Final safety check.
		if (current > end) {
			return NssError.BufferOutOfRange;
		}

		Marshal.StructureToPtr(resultStruct, resultStructPtr, fDeleteOld: false);
		return NssError.None;
	} // PackGroup

	private unsafe NssError PackMemberships(UInt32[] groupIds, IntPtr groupCountStartPtr, IntPtr groupBufferSizePtr, IntPtr groupsArrayPtr, Int64 limitValue) {
		if (groupIds == null) {
			groupIds = Array.Empty<UInt32>();
		}

		if (groupCountStartPtr == IntPtr.Zero || groupBufferSizePtr == IntPtr.Zero || groupsArrayPtr == IntPtr.Zero) {
			return NssError.NoMemory;
		}

		IntPtr startVal = Marshal.ReadIntPtr(groupCountStartPtr);
		IntPtr sizeVal = Marshal.ReadIntPtr(groupBufferSizePtr);
		Int64 start = startVal.ToInt64();
		Int64 size = sizeVal.ToInt64();

		// Deduplicate group IDs
		HashSet<UInt32> unique = new HashSet<UInt32>(groupIds);
		Int32 needed = unique.Count;
		if (limitValue >= 0 && needed > limitValue) {
			needed = (Int32)limitValue;
		}

		if (start + needed > size) {
			Marshal.WriteIntPtr(groupBufferSizePtr, new IntPtr(start + needed));
			return NssError.BufferOutOfRange;	// ERANGE
		}

		IntPtr groupsArray = Marshal.ReadIntPtr(groupsArrayPtr);
		if (groupsArray == IntPtr.Zero && needed > 0) {
			return NssError.NoMemory;
		}

		UInt32* grps = (UInt32*)groupsArray;
		Int32 index = 0;
		foreach (UInt32 gid in unique) {
			if (start + index >= size) break;
			if (limitValue >= 0 && index >= limitValue) break;
			grps[start + index] = gid;
			index++;
		}

		Marshal.WriteIntPtr(groupCountStartPtr, new IntPtr(start + index));
		return NssError.None;
	} // PackMemberships
	#endregion

} // NssProviderBase
