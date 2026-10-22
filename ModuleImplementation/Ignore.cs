using System;
using System.IO;
using System.Linq;
using System.Text;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// Ignore local users and local groups.
/// </summary>
public class Ignore {
	private static FileInfo LocalUsersFile = new FileInfo("/etc/passwd");
	private static FileInfo LocalGroupsFile = new FileInfo("/etc/group");

	/// <summary>
	/// Instantiates a new ignore users and groups instance.
	/// </summary>
	public Ignore() {
	} // Ignore

	public Boolean IgnoreUser(String userName) {
		if (Ignore.LocalUsersFile.Exists == false) {
			return false;
		}
		if (String.IsNullOrWhiteSpace(userName) == true) {
			return false;
		}

		// Example: "root:x:0:0:Super User:/root:/bin/bash".
		using FileStream fileStream = File.OpenRead(Ignore.LocalUsersFile.FullName);
		using StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8);
		String line = streamReader.ReadLine();
		while (line != null) {
			if (line.StartsWith($"{userName}:") == true) {
				return true;
			}
			line = streamReader.ReadLine();
		}

		return false;
	} // IgnoreUser

	public Boolean IgnoreUser(UInt32 userId) {
		if (Ignore.LocalUsersFile.Exists == false) {
			return false;
		}

		// Example: "root:x:0:0:Super User:/root:/bin/bash".
		using FileStream fileStream = File.OpenRead(Ignore.LocalUsersFile.FullName);
		using StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8);
		String line = streamReader.ReadLine();
		while (line != null) {
			if (line.Split(':').ElementAtOrDefault(2) == userId.ToString()) {
				return true;
			}
			line = streamReader.ReadLine();
		}

		return false;
	} // IgnoreUser

	public Boolean IgnoreGroup(String groupName) {
		if (Ignore.LocalGroupsFile.Exists == false) {
			return false;
		}
		if (String.IsNullOrWhiteSpace(groupName) == true) {
			return false;
		}

		// Example: "root:x:0:brltty,root".
		using FileStream fileStream = File.OpenRead(Ignore.LocalGroupsFile.FullName);
		using StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8);
		String line = streamReader.ReadLine();
		while (line != null) {
			if (line.StartsWith($"{groupName}:") == true) {
				return true;
			}
			line = streamReader.ReadLine();
		}

		return false;
	} // IgnoreGroup

	public Boolean IgnoreGroup(UInt32 groupId) {
		if (Ignore.LocalGroupsFile.Exists == false) {
			return false;
		}

		// Example: "root:x:0:brltty,root".
		using FileStream fileStream = File.OpenRead(Ignore.LocalGroupsFile.FullName);
		using StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8);
		String line = streamReader.ReadLine();
		while (line != null) {
			if (line.Split(':').ElementAtOrDefault(2) == groupId.ToString()) {
				return true;
			}
			line = streamReader.ReadLine();
		}

		return false;
	} // IgnoreGroup

} // Ignore
