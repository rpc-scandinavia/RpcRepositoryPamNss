## RPC Repository PAM/NSS module

This is a PAM and NSS module written in C# using DotNet AOT.
Everything is designed, so you can copy the project and just implement your business logic:

1) Copy project.
2) Remove everything in `ModuleImplementation`.
3) Create a PAM implementation that extends `PamProviderBase` and a NSS implementation that extends `NssProviderBase`.
4) Configure three project properties:
   * `PamProviderClass` the name of your PAM class.
   * `NssProviderClass` the name of your NSS class.
   * `NssServiceName` the name of your published native AOT library **.so** file.
5) Remove my references to gRPC in the project configuration. 
6) Publish with `dotnet  publish  --runtime "linux-x64"  --configuration "Release"  "RpcRepositoryPamNss.csproj"`

Note that the native library, is only build when you publish.

## Hello World

A "hello world" style implementation with static data, are included in `TestPamProvider.cs` and `TestNssProvider.cs` 
and `TestData.cs`.

## Design

I designed this project to be easy to reuse:

* **Base classes** that handle all the low-level unpacking/packing of data between GNU C and DotNet in 
`ProviderBase`, `PamProviderBase` and `NssProviderBase`. They include virtual methods, that provide the data they 
need to handle PAM and serve NSS requests.
* **Source generator** in `NativeExportGenerator.cs`, because the NSS expect the native library name to be part of the 
export name (entry point), and there is no way to use DotNet project properties in the code. Also, it is necessary to 
instantiate the provider class, so I choose a source generator to make the project eacy to copy and reuse.

### PAM

If you are new to PAM development, you can see the basic flow in the `TestPamProvider` class.
Remember that PAM is a chain of modules, and one module can leave data for the next module, and thus it is possible 
for **KDE Wallet** to authenticate the user, because a previous module left the password. Also, the module may be 
called multiple times during sign-in, typically for authentication and when the session is opened.
There are some standard variables that you can set and get from PAM (`PamItemType`), and if you need to pass data 
from _authentication_ to _open session_, you can do this as well:

* `GetItemString`.
* `SetItemString`.
* `GetDataString`, `GetDataInt32` and `GetDataByteArray`.
* `SetDataString`, `SetDataInt32` and `SetDataByteArray`.

Authentication flow:

1) PAM requests user authentication, and should provide the **username**, which you get with `GetUser`.
2) You get the password with `GetItemString`, in case a previous module requested it from the user.
3) If not:
   * You request it from PAM with `PromptUser`.
   * You validate the user against your data.
   * You set the valid password in PAM with `SetItemString` and `PamItemType.PAM_AUTHTOK`.   

### NSS

NSS is more simple in regard of returning data. The major issue with NSS is race conditions and timeouts. I use a 
`NssCache` that uses a lock on each **username**, **user id** etc. that NSS requests, and that was the difference 
between unusable and sitting at the table with chips to play.

## Native exports and the source generator

As an alternative to a global using alias, when specifying the PAM/NSS provider class in each export, and an 
alternative to manually edit the NSS export EntryPoints when the output library name changes, a source generator is 
used to generate the two classes, configured in the project file.

Modify those properties in the project file:

```xml
<PamProviderClass>TestPamProvider</PamProviderClass>
<NssProviderClass>TestNssProvider</NssProviderClass>
<NssServiceName>RpcRepositoryPamNss</NssServiceName>
```

The source generator is only one project file `RpcRepositoryPamNssNativeExportsGenerator.csproj` and one source file `NativeExportsGenerator/NativeExportGenerator.cs`.

## Testing

Install the module and configure, typically in `/etc/pam.d` and `/etc/nsswitch.conf`.

Follow the log: `journalctl  --follow  --grep="PAM"  --grep="NSS"`

To test the module without other PAM modules in the chain, create PAM configuration file in `/etc/pam.d/rpc-test`: 

```
auth      required  RpcRepositoryPamNss.so
account   required  RpcRepositoryPamNss.so
password  required  RpcRepositoryPamNss.so
session   required  RpcRepositoryPamNss.so
```

Test PAM configuration with the following commands:

```bash
pamtester  rpc-test  "tester"  authenticate
pamtester  rpc-test  "tester"  authenticate  acct_mgmt  open_session
```

Test NSS with the following commands, you can replace the username with the userid:

```bash
id
id  "tester"
id  -G  "tester"
groups
getent  passwd
getent  passwd  "tester"
getend  group
getend  group  "testers"
```

## Debugging

### Verify the exports in the native library

Inspect the exports with this command:

```bash
nm  --dynamic  "bin/Release/net10.0/linux-x64/native/RpcRepositoryPamNss.so"  |  grep  --extended-regexp  "(pam|nss)"
```

Verify that the exports are without version numbers. This is fixed in the project file, by performing a search and 
replace for the version numbers in the `module.exports` file.

Also verify that the name of the module file is part of the NSS exports, that is the module file as it is installed 
on the system, and not as it is build. You man rename it afterwards.

```text
000000000012b0c0 T _nss_rpc-repository_endgrent
000000000012a9d0 T _nss_rpc-repository_endpwent
000000000012adb0 T _nss_rpc-repository_endspent
000000000012b170 T _nss_rpc-repository_getgrent_r
000000000012b320 T _nss_rpc-repository_getgrgid_r
000000000012b240 T _nss_rpc-repository_getgrnam_r
000000000012aa80 T _nss_rpc-repository_getpwent_r
000000000012ab50 T _nss_rpc-repository_getpwnam_r
000000000012ac30 T _nss_rpc-repository_getpwuid_r
000000000012ae60 T _nss_rpc-repository_getspent_r
000000000012af30 T _nss_rpc-repository_getspnam_r
000000000012b3f0 T _nss_rpc-repository_initgroups_dyn
000000000012b010 T _nss_rpc-repository_setgrent
000000000012a920 T _nss_rpc-repository_setpwent
000000000012ad00 T _nss_rpc-repository_setspent
000000000012a700 T pam_sm_acct_mgmt
000000000012a620 T pam_sm_authenticate
000000000012a850 T pam_sm_chauthtok
000000000012a7e0 T pam_sm_close_session
000000000012a770 T pam_sm_open_session
000000000012a690 T pam_sm_setcred
```

### Verfiy linking

Inspect the links with this command:

```bash
ldd  "bin/Release/net10.0/linux-x64/native/RpcRepositoryPamNss.so"
```

Verify that all are linked to a library on the system.

```text
linux-vdso.so.1 (0x00007f24e39b3000)
libm.so.6 => /usr/lib/libm.so.6 (0x00007f24e2cc9000)
libc.so.6 => /usr/lib/libc.so.6 (0x00007f24e2aa6000)
/usr/lib64/ld-linux-x86-64.so.2 (0x00007f24e39b5000)
```

### PAM

On some Debian based systems, you can enable/disable PAM modules with this command:

```bash
/usr/sbin/pam-auth-update
```

### Permissions

The following SystemD services are in play during sign-in (on my KDE Linux system):

* accounts-daemon.service.d 
* polkit-agent-helper@.service.d 
* polkit.service.d 
* systemd-userdbd.service.d

If they are not happy, the system might not sign in any user, or in worst case not even boot properly!

My solution was to write SystemD service overrides for each of them, allowing access to the network (for gRPC).
Example `/usr/lib/systemd/system/polkit.service.d/80-rpc-repository.conf`:

```ini
[Service]
DevicePolicy            = closed
MemoryMax               = 192M
MemorySwapMax           = 192M

# Enable networking and IP socket families.
PrivateNetwork          = false
RestrictAddressFamilies = AF_UNIX AF_INET AF_INET6
IPAddressDeny           = any
IPAddressAllow          = localhost
IPAddressAllow          = 127.0.0.0/8
IPAddressAllow          = ::1
IPAddressAllow          = fd00:3::/64
IPAddressAllow          = 10.10.3.0/24
```

### Forked process

Especially with NSS, the module is loaded into the memory space of the using process, and that causes problems when 
that process if forked, and the child process continue to use the module.

One major issue I found using a DotNet AOT module, is that when a process if forked, some of the module internals 
fail. The easy reproducible example is that `System.Environment.ProcessId` return the process id of the parent 
process and not the forked child process, which you can get with `[DllImport("libc", EntryPoint = "getpid")] public 
static extern Int32 GetPid();`.  

Then imagine what **static** fields/properties in the module and its networking/gRPC code, that suddenly have the wrong 
state after a fork.

My solution was to write a helper program in DotNet AOT that can do the gRPC communication. Then I can run that program 
in a clean non-forked process.

But even `System.Process` with `RedirectStandardOutput = true` and `RedirectStandardError = true` failed to capture 
the output of the helper program. Here the solution was to execute the program using **DllImport**s. I used AI to 
write a class that can execute a program and capture standard output and standard error, see that in `PosixExecute.cs`.
