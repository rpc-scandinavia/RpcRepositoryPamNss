#!/usr/bin/python

import ctypes
import os

so_path = "/home/rpc@rpc-scandinavia.dk/Development/Solutions/RpcRepositoryPamNss/bin/Release/net10.0/linux-x64/native/RpcRepositoryPamNss.so"
nss_service = "rpc-repository"


if not os.path.exists(so_path):
    print(f"Error: Library not found at:\n{so_path}")
    exit(1)

print(f"Loading shared library from:\n{so_path}")
lib = ctypes.CDLL(so_path)

# Locate the initgroups_dyn symbol
func_name = f"_nss_{nss_service}_initgroups_dyn"
if not hasattr(lib, func_name):
    # Fallback search if a prefix is present
    symbols = [name for name in dir(lib) if "initgroups_dyn" in name]
    if symbols:
        func_name = symbols[0]
        print(f"Found dynamic symbol: {func_name}")
    else:
        print("Error: Could not find any initgroups_dyn symbol in the shared library.")
        exit(1)

initgroups_dyn = getattr(lib, func_name)

# Define the C-compatible function signature matching glibc's initgroups_dyn:
# enum nss_status _nss_*_initgroups_dyn(const char *user, gid_t group, long int *start, long int *size, gid_t **groupsp, long int limit, int *errnop)
initgroups_dyn.argtypes = [
    ctypes.c_char_p,                  # user
    ctypes.c_uint32,                  # group (primary gid)
    ctypes.POINTER(ctypes.c_int64),   # start (pointer to counter/start index)
    ctypes.POINTER(ctypes.c_int64),   # size (pointer to buffer capacity)
    ctypes.POINTER(ctypes.c_void_p),  # groupsp (pointer to the group array pointer)
    ctypes.c_int64,                   # limit
    ctypes.POINTER(ctypes.c_int32)    # errnop
]
initgroups_dyn.restype = ctypes.c_int # NssStatus enum return value

# Test inputs
username = b"rpc@rpc-scandinavia.dk"
primary_group = ctypes.c_uint32(1000)

start_val = ctypes.c_int64(0)
size_val = ctypes.c_int64(3)  # Test with a deliberately small buffer size to check handling

# Allocate a small managed C array for groups (e.g., 3 slots of uint32)
group_array_type = ctypes.c_uint32 * 3
group_buffer = group_array_type(0, 0, 0)
groupsp_val = ctypes.c_void_p(ctypes.addressof(group_buffer))

limit_val = ctypes.c_int64(-1)
errno_val = ctypes.c_int32(0)

print(f"\n--- Executing {func_name} ---")
print(f"User: {username.decode()}")
print(f"Initial start: {start_val.value}, size: {size_val.value}")

status = initgroups_dyn(
    username,
    primary_group,
    ctypes.byref(start_val),
    ctypes.byref(size_val),
    ctypes.byref(groupsp_val),
    limit_val,
    ctypes.byref(errno_val)
)

print(f"\n--- Execution Results ---")
print(f"NssStatus Return Code: {status} (0=Success, 3=TryAgain/ERANGE)")
print(f"Updated start position / count: {start_val.value}")
print(f"Updated required buffer size: {size_val.value}")
print(f"Error number (errno): {errno_val.value}")

# Read back any groups written into the buffer
actual_ptr = groupsp_val.value
if start_val.value > 0 and actual_ptr:
    fetched_groups = []
    for i in range(min(start_val.value, size_val.value)):
        gid = ctypes.c_uint32.from_address(actual_ptr + i * ctypes.sizeof(ctypes.c_uint32)).value
        fetched_groups.append(gid)
    print(f"Retrieved Group IDs: {fetched_groups}")
else:
    print("No groups written or buffer reallocation requested.")
