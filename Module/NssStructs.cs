using System;
using System.Runtime.InteropServices;
namespace RpcScandinavia.Repository.PamNss.Internal;

/// <summary>
/// User data packed for native return.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PasswdStruct {
    public IntPtr pw_name;
    public IntPtr pw_passwd;
    public UInt32 pw_uid;
    public UInt32 pw_gid;
    public IntPtr pw_gecos;
    public IntPtr pw_dir;
    public IntPtr pw_shell;
} // PasswdStruct

/// <summary>
/// Shadow data packed for native return.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ShadowStruct {
    public IntPtr sp_namp;				// Login name.
    public IntPtr sp_pwdp;				// Encrypted password hash.
    public Int64 sp_lstchg;				// Date of last change (days since Jan 1, 1970).
    public Int64 sp_min;				// Minimum days between changes.
    public Int64 sp_max;				// Maximum days between changes.
    public Int64 sp_warn;				// Days before expiration to warn.
    public Int64 sp_inact;				// Days after expiration before account is locked.
    public Int64 sp_expire;				// Absolute expiration date (days since epoch).
    public UInt64 sp_flag;				// Reserved / flags.
} // ShadowStruct

/// <summary>
/// Group data packed for native return.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct GroupStruct {
    public IntPtr gr_name;
    public IntPtr gr_passwd;
    public UInt32 gr_gid;
    public IntPtr gr_mem;				// Pointer to a null-terminated array of char* pointers (char**).
} // GroupStruct
