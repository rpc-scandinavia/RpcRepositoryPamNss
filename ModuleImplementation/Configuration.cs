using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
namespace RpcScandinavia.Repository.PamNss;

/*
# RPC Repository PAM/NSS Module Configuration
host = fd00:3::11
port = 50051
certificate_file = /etc/rpc-repository/rpc-repository.cer
certificate_common_name = rpc-repository-server-host
tenant = rpc-scandinavia.dk
update_file = /tmp/rpc-repository.so
update_pam_file = /tmp/pam-test.so					"/usr/lib/lib/x86_64-linux-gnu/security"
update_nss_file = /tmp/libnss_pam-test.so			"/usr/lib/lib/x86_64-linux-gnu/libnss_LIBRARY.so.2"
log = info

ignore_passwd = true
ignore_group = true

dataclient = /usr/lib/rpc-scandinavia/rpc-repository/RpcRepositoryPamNssDataClient
dataclient_max_attempts = 3
*/
/// <summary>
/// The module configuration, read from the configuration file.
/// The configuration file is formatted as lines with "name = value", and comments using "#".
/// </summary>
public class Configuration {
	// Non-configurable options.
	public readonly String DataClientMarkerKey = "RPC_REPOSITORY_NSS_DATA_CLIENT";
	public readonly String DataClientMarkerValue = "1";
	public readonly Int32 DataClientCallDeadlineMilliseconds = 5000;
	public readonly TimeSpan DataClientBackoffMilliseconds = TimeSpan.FromMilliseconds(100);
	public readonly TimeSpan DataClientConnectTimeout = TimeSpan.FromSeconds(2);
	public readonly TimeSpan DataClientKeepAlivePingDelay = TimeSpan.FromSeconds(30);			// Send an HTTP/2 ping frame if the connection is idle for 30 seconds.
	public readonly TimeSpan DataClientKeepAlivePingTimeout = TimeSpan.FromSeconds(5);			// Wait up to 5 seconds for the server to acknowledge the ping.

	// Configuration keys.
	private const String KeyHost = "host";
	private const String KeyPort = "port";
	private const String KeyCertificateFile = "certificate_file";
	private const String KeyCertificateCommonName = "certificate_common_name";
	private const String KeyTenants = "tenant";
	private const String KeyUpdateFile = "update_file";
	private const String KeyUpdatePamFile = "update_pam_file";
	private const String KeyUpdateNssFile = "update_nss_file";
	private const String KeyLogLevel = "log";
	private const String KeyIgnorePasswd = "ignore_passwd";
	private const String KeyIgnoreGroup = "ignore_group";
	private const String KeyDataClient = "dataclient";
	private const String KeyDataClientMaxAttempts = "dataclient_max_attempts";

	// Default configuration values.
	private static FileInfo DefaultConfigurationFile = new FileInfo("/etc/rpc-repository/rpc-repository.conf");
	private static Int32 DefaultPort = 50051;
	private static FileInfo DefaultCertificateFile = new FileInfo("/etc/rpc-repository/rpc-repository.cer");
	private static String DefaultCertificateCommonName = "rpc-repository";
	private static String[] DefaultTenants = ["test"];
	private static FileInfo DefaultUpdateFile = new FileInfo("/tmp/rpc-repository.so");
	private static FileInfo DefaultUpdatePamFile = new FileInfo("/tmp/rpc_pam-test.so");
	private static FileInfo DefaultUpdateNssFile = new FileInfo("/tmp/rpc_libnss_pam-test.so");
	private static LogLevel DefaultLogLevel = LogLevel.Information;
	private static FileInfo DefaultDataClientFile = new FileInfo("/usr/lib/rpc-scandinavia/rpc-repository/RpcRepositoryPamNssDataClient");
	private static Int32 DefaultDataClientMaxAttempts = 3;

	// Loaded configuration keys and values.
	private readonly Dictionary<String, String> options;

	#region Methods
	//------------------------------------------------------------------------------------------------------------------
	// Methods.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Instantiate a new configuration object, and try to read the options from the default configuration file.
	/// </summary>
	public Configuration() {
		this.options = new Dictionary<String, String>();
		this.Load(Configuration.DefaultConfigurationFile);
	} // Configuration

	/// <summary>
	/// Load all options from the configuration file.
	/// </summary>
	/// <param name="configurationFile">The configuration file.</param>
	private void Load(FileInfo file) {
		// Validate.
		if ((file == null) || (file.Exists == false)) {
			return;
		}

		// Parse "key = value" pairs.
		this.options.Clear();
		foreach (String fileLine in File.ReadAllLines(file.FullName)) {
			// Trim the whitespaces.
			String line = fileLine.Trim();

			// Skip comments.
			if (line.StartsWith('#') == true) {
				continue;
			}

			// Remove line comment.
			Int32 commentIndex = line.IndexOf('#');
			if (commentIndex > -1) {
				line = line.Substring(commentIndex);
			}

			// Skip non "key = value" lines.
			Int32 separatorIndex = line.IndexOf('=');
			if (separatorIndex < 1) {
				continue;
			}

			// Add the key and non-empty value.
			String key = line.Substring(0, separatorIndex).Trim();
			String value = line.Substring(separatorIndex + 1).Trim();
			if ((this.options.ContainsKey(key) == false) && (String.IsNullOrWhiteSpace(value) == false)) {
				this.options.Add(key, value);
			}
		}
	} // Load

	/// <summary>
	/// Write the configuration to the log.
	/// </summary>
	/// <param name="options">The configuration.</param>
	public override String ToString() {
		return
			$$"""
			RPC Repository PAM/NSS Module Configuration:
			* Host: '{{this.Host}}'
			* Port: '{{this.Port}}'
			* Certificate file: '{{this.CertificateFile.FullName}}'
			* Certificate Common Name: '{{this.CertificateCommonName}}'
			* Tenants identifier: '{{this.Tenants}}'
			* Log level: '{{this.LogLevel}}'
			* Ignore local users: '{{this.IgnoreLocalUsers}}'
			* Ignore local groups: '{{this.IgnoreLocalGroups}}'
			* DataClient executable: '{{this.DataClient}}'
			* DataClient max. attempts: '{{this.DataClientMaxAttempts}}'
			""";
	} // ToString
	#endregion

	#region Properties
	//------------------------------------------------------------------------------------------------------------------
	// Properties.
	//------------------------------------------------------------------------------------------------------------------
	/// <summary>
	/// Gets the IP address of the RPC Repository server, or none.
	/// </summary>
	public IPAddress Host {
		get {
			if ((this.options.TryGetValue(Configuration.KeyHost, out String value) == true) &&
				(IPAddress.TryParse(value, out IPAddress address) == true)) {
				return address;
			} else {
				return IPAddress.None;
			}
		}
	} // Host

	/// <summary>
	/// Gets the network port, or default 50051.
	/// </summary>
	public Int32 Port {
		get {
			if ((this.options.TryGetValue(Configuration.KeyPort, out String value) == true) &&
				(Int32.TryParse(value, out Int32 port) == true) &&
				(port > 1000) &&
				(port < 65536)) {
				return port;
			} else {
				return Configuration.DefaultPort;
			}
		}
	} // Port

	/// <summary>
	/// Gets the URI of the RPC Repository server, or null.
	/// </summary>
	public Uri GetHostAndPort {
		get {
			try {
				if (this.Host.AddressFamily == AddressFamily.InterNetworkV6) {
					return new Uri($"https://[{this.Host}]:{this.Port}");
				} else {
					return new Uri($"https://{this.Host}:{this.Port}");
				}
			} catch {
				return null;
			}
		}
	} // GetHostAndPort

	/// <summary>
	/// Gets the certificate file.
	/// </summary>
	public FileInfo CertificateFile {
		get {
			try {
				return new FileInfo(this.options[Configuration.KeyCertificateFile]);
			} catch {
				return Configuration.DefaultCertificateFile;
			}
		}
	} // CertificateFile

	/// <summary>
	/// Gets the certificate.
	/// </summary>
	public X509Certificate2 Certificate {
		get {
			try {
				return X509CertificateLoader.LoadCertificateFromFile(this.options[Configuration.KeyCertificateFile]);
			} catch {
				return null;
			}
		}
	} // Certificate

	/// <summary>
	/// Gets the certificate common name.
	/// </summary>
	public String CertificateCommonName {
		get {
			if (this.options.TryGetValue(Configuration.KeyCertificateCommonName, out String value) == true) {
				return value;
			} else {
				return Configuration.DefaultCertificateCommonName;
			}
		}
	} // CertificateCommonName

	/// <summary>
	/// Gets the tenant identifiers.
	/// </summary>
	public String[] Tenants {
		get {
			if (this.options.TryGetValue(Configuration.KeyTenants, out String value) == true) {
				// Currently only one tenant is supported.
				return [value];
			} else {
				return Configuration.DefaultTenants;
			}
		}
	} // Tenants

	/// <summary>
	/// Gets the update file.
	/// </summary>
	public FileInfo UpdateFile {
		get {
			try {
				return new FileInfo(this.options[Configuration.KeyUpdateFile]);
			} catch {
				return Configuration.DefaultUpdateFile;
			}
		}
	} // UpdateFile

	/// <summary>
	/// Gets the update PAM file.
	/// The library configured in PAM, typically located in "/usr/lib/lib/x86_64-linux-gnu/security".
	/// </summary>
	public FileInfo UpdatePamFile {
		get {
			try {
				return new FileInfo(this.options[Configuration.KeyUpdatePamFile]);
			} catch {
				return Configuration.DefaultUpdatePamFile;
			}
		}
	} // UpdatePamFile

	/// <summary>
	/// Gets the update NSS file.
	/// The library configured in "nsswitch.conf", typically located in "/usr/lib/lib/x86_64-linux-gnu/libnss_LIBRARY.so.2".
	/// </summary>
	public FileInfo UpdateNssFile {
		get {
			try {
				return new FileInfo(this.options[Configuration.KeyUpdateNssFile]);
			} catch {
				return Configuration.DefaultUpdateNssFile;
			}
		}
	} // UpdateNssFile

	/// <summary>
	/// Gets the logging level.
	/// </summary>
	public LogLevel LogLevel {
		get {
			if (this.options.TryGetValue(Configuration.KeyLogLevel, out String value) == true) {
				return value switch {
					("info" or "information") => LogLevel.Information,
					("warn" or "warning") => LogLevel.Warning,
					("error") => LogLevel.Error,
					("debug") => LogLevel.Debug,
					("trace") => LogLevel.Trace,
					_ => Configuration.DefaultLogLevel,
				};
			} else {
				return Configuration.DefaultLogLevel;
			}
		}
	} // LogLevel

	/// <summary>
	/// Gets whether local users should be ignored.
	/// </summary>
	public Boolean IgnoreLocalUsers {
		get {
			if ((this.options.TryGetValue(Configuration.KeyIgnorePasswd, out String value) == true) &&
				(Boolean.TryParse(value, out Boolean ignore) == true)) {
				return ignore;
			} else {
				return true;
			}
		}
	} // IgnoreLocalUsers

	/// <summary>
	/// Gets whether local groups should be ignored.
	/// </summary>
	public Boolean IgnoreLocalGroups {
		get {
			if ((this.options.TryGetValue(Configuration.KeyIgnoreGroup, out String value) == true) &&
				(Boolean.TryParse(value, out Boolean ignore) == true)) {
				return ignore;
			} else {
				return true;
			}
		}
	} // IgnoreLocalGroups

	/// <summary>
	/// Gets the full path to the data client executable (helper that perform gRPC communication in forked processes).
	/// </summary>
	public FileInfo DataClient {
		get {
			try {
				return new FileInfo(this.options[Configuration.KeyDataClient]);
			} catch {
				return Configuration.DefaultDataClientFile;
			}
		}
	} // DataClient

	/// <summary>
	/// Gets the maximum number of times the data client retry gRPC calls.
	/// </summary>
	public Int32 DataClientMaxAttempts {
		get {
			if ((this.options.TryGetValue(Configuration.KeyDataClientMaxAttempts, out String value) == true) &&
				(Int32.TryParse(value, out Int32 retries) == true) &&
				(retries > -1)) {
				return retries;
			} else {
				return Configuration.DefaultDataClientMaxAttempts;
			}
		}
	} // DataClientMaxAttempts
	#endregion

} // Configuration
