using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// Update handler.
/// This can replace the update file, with the data from a byte array.
/// After the data is successfully saved in the update versioned file and validated, the update file, the PAM update file
/// and the NSS update file are linked to the new update versioned file.
/// </summary>
public class UpdateHandler {
	private readonly ILogger logger;
	private readonly Version version;
	private readonly FileInfo updateFile;
	private readonly FileInfo updatePamFile;
	private readonly FileInfo updateNssFile;

	/// <summary>
	/// Instantiates a new update handler.
	/// </summary>
	/// <param name="logger">The logger.</param>
	/// <param name="updateFile">The destination update data file.</param>
	/// <param name="updatePamFile">The update PAM file link.</param>
	/// <param name="updateNssFile">The update NSS file link.</param>
	public UpdateHandler(ILogger logger, FileInfo updateFile, FileInfo updatePamFile, FileInfo updateNssFile) {
		this.logger = logger;
		this.version = Assembly.GetExecutingAssembly().GetName().Version;
		this.updateFile = updateFile;
		this.updatePamFile = updatePamFile;
		this.updateNssFile = updateNssFile;

		// Follow the links, for each update file.
		try {
			FileSystemInfo finalUpdateFile = updateFile.ResolveLinkTarget(true);
			if ((finalUpdateFile != null) && (finalUpdateFile is FileInfo) && (finalUpdateFile.Exists == true)) {
				this.updateFile = (FileInfo)finalUpdateFile;
			}
		} catch {}
		try {
			FileSystemInfo finalUpdatePamFile = updatePamFile.ResolveLinkTarget(true);
			if ((finalUpdatePamFile != null) && (finalUpdatePamFile is FileInfo) && (finalUpdatePamFile.Exists == true)) {
				this.updatePamFile = (FileInfo)finalUpdatePamFile;
			}
		} catch {}
		try {
			FileSystemInfo finalUpdateNssFile = updateNssFile.ResolveLinkTarget(true);
			if ((finalUpdateNssFile != null) && (finalUpdateNssFile is FileInfo) && (finalUpdateNssFile.Exists == true)) {
				this.updateNssFile = (FileInfo)finalUpdateNssFile;
			}
		} catch {}
	} // UpdateHandler

	/// <summary>
	/// Saves the update data in a new update version file (update file name with version), and validate the new file
	/// against the SHA-256 update data checksum, and validate the new file version by calling its native "get_module_version"
	/// method to get its version, and compare that version against the expected update version.
	///
	/// If everything is valid, the update file, the PAM update file and the NSS update file are linked to the new update
	/// versioned file.
	/// </summary>
	/// <param name="userName">The username.</param>
	/// <param name="updateVersion">The version of the update data.</param>
	/// <param name="updateDataChecksum">The checksum of the update data.</param>
	/// <param name="updateData">The update data.</param>
	/// <returns>Whether the update file was replaced with the update data.</returns>
	/// <exception cref="ArgumentException">When the username is null or empty, when the update checksum is null or empty, when the update data is empty.</exception>
	/// <exception cref="ArgumentNullException">When the update version is null, when the update data is null.</exception>
	/// <exception cref="DirectoryNotFoundException">When the update file directory don't exist, the update PAM file directory don't exist or the update NSS file directory don't exist.</exception>
	public Boolean Update(String userName, Version updateVersion, String updateDataChecksum, Byte[] updateData) {
		try {
			// Validate.
			ArgumentException.ThrowIfNullOrWhiteSpace(userName);
			ArgumentNullException.ThrowIfNull(updateVersion);
			ArgumentNullException.ThrowIfNull(updateData);
			if (updateData.Length == 0) {
				throw new ArgumentException($"'{nameof(updateData)}' is empty.");
			}
			if (this.updateFile.Directory?.Exists == false) {
				throw new DirectoryNotFoundException(this.updateFile.Directory.FullName);
			}
			if (this.updatePamFile.Directory?.Exists == false) {
				throw new DirectoryNotFoundException(this.updatePamFile.Directory.FullName);
			}
			if (this.updateNssFile.Directory?.Exists == false) {
				throw new DirectoryNotFoundException(this.updateNssFile.Directory.FullName);
			}

			// Log information.
			if (this.logger.IsEnabled(LogLevel.Information) == true) {
				this.logger.LogInformation(
					$$"""
					RPC Repository PAM/NSS Module Update:
					* User: '{{userName}}'
					* File: '{{this.updateFile.FullName}}' 
					* PAM File: '{{this.updatePamFile.FullName}}'
					* PAM File: '{{this.updateNssFile.FullName}}'
					* Data: {{updateData.Length}} bytes.
					* Checksum: '{{updateDataChecksum}}' (sha256).
					* Version: {{this.version}} => {{updateVersion}}
					"""
				);
			}

			// Don't update when the versions are the same.
			if (this.version == updateVersion) {
				if (this.logger.IsEnabled(LogLevel.Information) == true) {
					this.logger.LogInformation($"'{userName}' update skipped because versions match.");
				}

				// Return.
				return false;
			}

			// Save the update data in a file where the version is part of the name.
			FileInfo updateVersionFile = new FileInfo(Path.Join(this.updateFile.DirectoryName, $"{Path.GetFileNameWithoutExtension(this.updateFile.Name)}-{updateVersion}{Path.GetExtension(this.updateFile.Name)}"));
			File.WriteAllBytes(updateVersionFile.FullName, updateData);
			updateVersionFile.UnixFileMode = //755
				UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
				UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
				UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

			// Validate the checksum.
			using (SHA256 sha256 = SHA256.Create()) {
				using (FileStream validateStream = File.OpenRead(updateVersionFile.FullName)) {
					Byte[] hashBytes = sha256.ComputeHash(validateStream);
					//Byte[] hashBytes = sha256.ComputeHash(updateData);
					String confirmChecksum = Convert.ToHexString(hashBytes);

					if (confirmChecksum != updateDataChecksum) {
						throw new Exception($"Checksum don't match '{confirmChecksum}'.");
					}
				}
			}

			// Validate the version.
			Version confirmVersion = UpdateHandler.ReadVersionFromSo(updateVersionFile);
			if ((confirmVersion == null) || (confirmVersion != updateVersion)) {
				throw new Exception($"Versions don't match '{confirmVersion}'.");
			}

			// The update data contained a native library, that matched the checksum and produces the expected version
			// string. Re-create links from the update version file to the update file, the upate PAM file and the update NSS file.
			Int32 linkCount = 0;

			try {
				this.updateFile.Delete();
				this.updateFile.CreateAsSymbolicLink(updateVersionFile.FullName);
				linkCount++;
			} catch (Exception exception) {
				if (this.logger.IsEnabled(LogLevel.Error) == true) {
					this.logger.LogError($"'{userName}' update module error. Unable to create link '{this.updateFile.FullName}': {exception.Message}");
				}
			}

			try {
				this.updatePamFile.Delete();
				this.updatePamFile.CreateAsSymbolicLink(updateVersionFile.FullName);
				linkCount++;
			} catch (Exception exception) {
				if (this.logger.IsEnabled(LogLevel.Error) == true) {
					this.logger.LogError($"'{userName}' update module error. Unable to create link '{this.updatePamFile.FullName}': {exception.Message}");
				}
			}

			try {
				this.updateNssFile.Delete();
				this.updateNssFile.CreateAsSymbolicLink(updateVersionFile.FullName);
				linkCount++;
			} catch (Exception exception) {
				if (this.logger.IsEnabled(LogLevel.Error) == true) {
					this.logger.LogError($"'{userName}' update module error. Unable to create link '{this.updateNssFile.FullName}': {exception.Message}");
				}
			}

			// Log.
			if ((linkCount == 3) && (this.logger.IsEnabled(LogLevel.Error) == true)) {
				this.logger.LogError($"'{userName}' update finished.");
			}

			// Return.
			return (linkCount == 3);
		} catch (Exception exception) {
			if (this.logger.IsEnabled(LogLevel.Error) == true) {
				this.logger.LogError($"'{userName}' update module error: {exception.Message}");
			}

			// Return.
			return false;
		}
	} // Update

	#region Native library version retrieving
	//------------------------------------------------------------------------------------------------------------------
	// Native library version retrieving.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Export method that gets the module version as "major.minor.revision".
	/// </summary>
	[UnmanagedCallersOnly(EntryPoint = "get_module_version")]
	private static IntPtr GetModuleVersion() {
		Version version = Assembly.GetExecutingAssembly().GetName().Version;
		return Marshal.StringToCoTaskMemUTF8(version?.ToString() ?? String.Empty);
	} // GetModuleVersion

	// Define a delegate matching the "GetModuleVersion" unmanaged function signature.
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	private delegate IntPtr GetModuleVersionDelegate();

	[DllImport("libdl.so.2", EntryPoint = "dlopen")]
	private static extern IntPtr DlOpen(String filename, Int32 flags);

	[DllImport("libdl.so.2", EntryPoint = "dlsym")]
	private static extern IntPtr DlSym(IntPtr handle, String symbol);

	[DllImport("libdl.so.2", EntryPoint = "dlclose")]
	private static extern Int32 DlClose(IntPtr handle);

	public static Version ReadVersionFromSo(FileInfo nativeLibraryFile) {
		// Validate.
		if ((nativeLibraryFile == null) || (nativeLibraryFile.Exists == false)) {
			return null;
		}

		// RTLD_LAZY = 1
		IntPtr handle = DlOpen(nativeLibraryFile.FullName, 1);
		if (handle == IntPtr.Zero) {
			return null;
		}

		try {
			IntPtr pointer = DlSym(handle, "get_module_version");
			if (pointer == IntPtr.Zero) {
				return null;
			}

			GetModuleVersionDelegate getVersion = Marshal.GetDelegateForFunctionPointer<GetModuleVersionDelegate>(pointer);
			IntPtr versionStringPointer = getVersion();

			return new Version(Marshal.PtrToStringUTF8(versionStringPointer));
		} catch {
			return null;
		} finally {
			DlClose(handle);
		}
	} // ReadVersionFromSo
	#endregion

} // UpdateHandler
