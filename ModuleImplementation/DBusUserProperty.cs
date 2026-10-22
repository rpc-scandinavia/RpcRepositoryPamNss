using System;
namespace RpcScandinavia.Repository.PamNss;

/// <summary>
/// The user properties that can be set using DBus.
/// </summary>
public enum DBusUserProperty {

	/// <summary>
	/// Set the users full name.
	/// Value type is <see cref="System.String"/>.
	/// </summary>
	FullName,

	/// <summary>
	/// Set the users e-mail address.
	/// Value type is <see cref="System.String"/>.
	/// </summary>
	Mail,

	/// <summary>
	/// Set the user language.
	/// Example: "de_DE.UTF-8".
	/// Value type is <see cref="System.String"/>.
	/// </summary>
	Language,

	/// <summary>
	/// Set the user location.
	/// Value type is <see cref="System.String"/>.
	/// </summary>
	Location,

	/// <summary>
	/// Set the users shell.
	/// Value type is <see cref="System.String"/>.
	/// </summary>
	Shell,

	/// <summary>
	/// Set the users home directory.
	/// Value type is <see cref="System.String"/>.
	/// </summary>
	Home,

	/// <summary>
	/// Set the user avatar.
	/// The file is copied to the final destination, usually "/var/lib/AccountsService/icons/username".
	/// Value type is an array of <see cref="System.Byte"/>, containing a PNG image.
	/// </summary>
	Avatar,

	/// <summary>
	/// Set the users account type as an integer: 0 = Standard user, 1 = Administrator
	/// Value type is <see cref="System.Int32"/>.
	/// </summary>
	AccountType,

	/// <summary>
	/// The new password mode, encoded as an integer: 0 = Regular password, 1 = Password must be set at next login, 2 = No password
	/// Value type is <see cref="System.Int32"/>.
	/// </summary>
	PasswordMode,

	/// <summary>
	/// Whether to enable automatic login for this user: true/false.
	/// Value type is <see cref="System.Boolean"/>.
	/// </summary>
	AutomaticLogin,

	/// <summary>
	/// Whether to lock or unlock the users account.
	/// Value type is <see cref="System.Boolean"/>.
	/// </summary>
	Locked,

} // DBusUserProperty
