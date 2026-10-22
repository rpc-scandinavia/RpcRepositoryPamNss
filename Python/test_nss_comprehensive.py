#!/usr/bin/python3

import ctypes
import os

so_path = "/home/rpc@rpc-scandinavia.dk/Development/Solutions/RpcRepositoryPamNss/bin/Release/net10.0/linux-x64/native/RpcRepositoryPamNss.so"
#so_path = "/home/rpc@rpc-scandinavia.dk/Development/Solutions/RpcRepositoryPamNss/libnss_rpc-repository.so.2"
#so_path = "/usr/lib/x86_64-linux-gnu/libnss_rpc-repository.so.2"
nss_service = "rpc-repository"
max_items = 20

if not os.path.exists(so_path):
    print(f"Error: Library not found at:\n{so_path}")
    exit(1)

print(f"Loading shared library with RTLD_GLOBAL from:\n{so_path}")
lib = ctypes.CDLL(so_path, mode=os.RTLD_GLOBAL)

# --- Define C Structs matching glibc ABI ---
class Passwd(ctypes.Structure):
    _fields_ = [
        ("pw_name", ctypes.c_char_p),
        ("pw_passwd", ctypes.c_char_p),
        ("pw_uid", ctypes.c_uint32),
        ("pw_gid", ctypes.c_uint32),
        ("pw_gecos", ctypes.c_char_p),
        ("pw_dir", ctypes.c_char_p),
        ("pw_shell", ctypes.c_char_p),
    ]

class Group(ctypes.Structure):
    _fields_ = [
        ("gr_name", ctypes.c_char_p),
        ("gr_passwd", ctypes.c_char_p),
        ("gr_gid", ctypes.c_uint32),
        ("gr_mem", ctypes.c_void_p),
    ]

class Spwd(ctypes.Structure):
    _fields_ = [
        ("sp_namp", ctypes.c_char_p),
        ("sp_pwdp", ctypes.c_char_p),
        ("sp_lstchg", ctypes.c_int64),
        ("sp_min", ctypes.c_int64),
        ("sp_max", ctypes.c_int64),
        ("sp_warn", ctypes.c_int64),
        ("sp_inact", ctypes.c_int64),
        ("sp_expire", ctypes.c_int64),
        ("sp_flag", ctypes.c_uint64),
    ]

def resolve_symbol(operation):
    preferred = f"_nss_{nss_service}_{operation}"
    try:
        return getattr(lib, preferred)
    except AttributeError:
        print(f"[!] Warning: Could not find symbol '{preferred}'")
        return None

# --- Bind NSS Functions ---
setpwent = resolve_symbol("setpwent")
endpwent = resolve_symbol("endpwent")
getpwent_r = resolve_symbol("getpwent_r")
getpwnam_r = resolve_symbol("getpwnam_r")
getpwuid_r = resolve_symbol("getpwuid_r")

setgrent = resolve_symbol("setgrent")
endgrent = resolve_symbol("endgrent")
getgrent_r = resolve_symbol("getgrent_r")
getgrnam_r = resolve_symbol("getgrnam_r")
getgrgid_r = resolve_symbol("getgrgid_r")

setspent = resolve_symbol("setspent")
endspent = resolve_symbol("endspent")
getspent_r = resolve_symbol("getspent_r")
getspnam_r = resolve_symbol("getspnam_r")

initgroups_dyn = resolve_symbol("initgroups_dyn")
if initgroups_dyn:
    initgroups_dyn.argtypes = [
        ctypes.c_char_p,
        ctypes.c_uint32,
        ctypes.POINTER(ctypes.c_int64),
        ctypes.POINTER(ctypes.c_int64),
        ctypes.POINTER(ctypes.c_void_p),
        ctypes.c_int64,
        ctypes.POINTER(ctypes.c_int32)
    ]
    initgroups_dyn.restype = ctypes.c_int

# Helper buffer for reentrant calls
buf_size = 8192
buffer = ctypes.create_string_buffer(buf_size)

# --- Wrapper Functions (Checking status == 1 for NSS_STATUS_SUCCESS) ---
def py_getpwnam(username):
    if not getpwnam_r: return None
    result = Passwd()
    errno = ctypes.c_int32(0)
    status = getpwnam_r(username.encode(), ctypes.byref(result), buffer, buf_size, ctypes.byref(errno))
    return result if status == 1 else None

def py_getpwuid(uid):
    if not getpwuid_r: return None
    result = Passwd()
    errno = ctypes.c_int32(0)
    status = getpwuid_r(ctypes.c_uint32(uid), ctypes.byref(result), buffer, buf_size, ctypes.byref(errno))
    return result if status == 1 else None

def py_getgrnam(groupname):
    if not getgrnam_r: return None
    result = Group()
    errno = ctypes.c_int32(0)
    status = getgrnam_r(groupname.encode(), ctypes.byref(result), buffer, buf_size, ctypes.byref(errno))
    return result if status == 1 else None

def py_getgrgid(gid):
    if not getgrgid_r: return None
    result = Group()
    errno = ctypes.c_int32(0)
    status = getgrgid_r(ctypes.c_uint32(gid), ctypes.byref(result), buffer, buf_size, ctypes.byref(errno))
    return result if status == 1 else None

def py_getspnam(username):
    if not getspnam_r: return None
    result = Spwd()
    errno = ctypes.c_int32(0)
    status = getspnam_r(username.encode(), ctypes.byref(result), buffer, buf_size, ctypes.byref(errno))
    return result if status == 1 else None


# --- 1. Iterate and Test Users ---
users_tested = []
if setpwent and getpwent_r and endpwent:
    print(f"\n{'='*10} ITERATING PASSWD (Max: {max_items}) {'='*10}")
    setpwent(0)
    count = 0

    while count < max_items:
        result = Passwd()
        errno = ctypes.c_int32(0)
        status = getpwent_r(ctypes.byref(result), buffer, buf_size, ctypes.byref(errno))

        if status != 1:
            break

        uname = result.pw_name.decode()
        uid = result.pw_uid
        gid = result.pw_gid
        users_tested.append((uname, uid, gid))
        print(f"[{count+1}] Enumerated User: {uname} (UID: {uid}, GID: {gid}, Shell: {result.pw_shell.decode()})")
        count += 1

    endpwent()

    print(f"\n--- Testing Individual User Lookups & Memberships ---")
    for uname, uid, gid in users_tested:
        nam_res = py_getpwnam(uname)
        uid_res = py_getpwuid(uid)
        sp_res = py_getspnam(uname)

        nam_match = "PASS" if nam_res and nam_res.pw_uid == uid else "FAIL"
        uid_match = "PASS" if uid_res and uid_res.pw_name.decode() == uname else "FAIL"
        shadow_status = f"PASS (LstChg: {sp_res.sp_lstchg})" if sp_res else "FAIL / NOT FOUND"

        # Test initgroups_dyn for memberships
        groups_list = []
        if initgroups_dyn:
            start_val = ctypes.c_int64(0)
            size_val = ctypes.c_int64(10)
            group_array_type = ctypes.c_uint32 * 10
            group_buffer = group_array_type(0)
            groupsp_val = ctypes.c_void_p(ctypes.addressof(group_buffer))
            limit_val = ctypes.c_int64(-1)
            err_val = ctypes.c_int32(0)

            ig_status = initgroups_dyn(
                uname.encode(),
                ctypes.c_uint32(gid),
                ctypes.byref(start_val),
                ctypes.byref(size_val),
                ctypes.byref(groupsp_val),
                limit_val,
                ctypes.byref(err_val)
            )
            if ig_status == 1 or start_val.value > 0:
                actual_ptr = groupsp_val.value
                if actual_ptr:
                    for i in range(min(start_val.value, size_val.value)):
                        g_id = ctypes.c_uint32.from_address(actual_ptr + i * ctypes.sizeof(ctypes.c_uint32)).value
                        groups_list.append(g_id)

        print(f"User '{uname}' -> getpwnam: {nam_match} | getpwuid: {uid_match} | getspnam: {shadow_status} | Memberships (GIDs): {groups_list}")


# --- 2. Iterate and Test Groups ---
if setgrent and getgrent_r and endgrent:
    print(f"\n{'='*10} ITERATING GROUP (Max: {max_items}) {'='*10}")
    setgrent(0)
    count = 0
    groups_tested = []

    while count < max_items:
        result = Group()
        errno = ctypes.c_int32(0)
        status = getgrent_r(ctypes.byref(result), buffer, buf_size, ctypes.byref(errno))

        if status != 1:
            break

        gname = result.gr_name.decode()
        gid = result.gr_gid
        groups_tested.append((gname, gid))
        print(f"[{count+1}] Enumerated Group: {gname} (GID: {gid})")
        count += 1

    endgrent()

    print(f"\n--- Testing Individual Group Lookups ---")
    for gname, gid in groups_tested:
        nam_res = py_getgrnam(gname)
        gid_res = py_getgrgid(gid)

        nam_match = "PASS" if nam_res and nam_res.gr_gid == gid else "FAIL"
        gid_match = "PASS" if gid_res and gid_res.gr_name.decode() == gname else "FAIL"
        print(f"Group '{gname}' (GID {gid}) -> getgrnam: {nam_match} | getgrgid: {gid_match}")


# --- 3. Iterate and Test Shadow ---
if setspent and getspent_r and endspent:
    print(f"\n{'='*10} ITERATING SHADOW (Max: {max_items}) {'='*10}")
    setspent(0)
    count = 0

    while count < max_items:
        result = Spwd()
        errno = ctypes.c_int32(0)
        status = getspent_r(ctypes.byref(result), buffer, buf_size, ctypes.byref(errno))

        if status != 1:
            break

        sname = result.sp_namp.decode() if result.sp_namp else "unknown"
        print(f"[{count+1}] Enumerated Shadow User: {sname} (Last Changed: {result.sp_lstchg})")
        count += 1

    endspent()

print(f"\n{'='*40}\nComprehensive validation completed successfully!")
