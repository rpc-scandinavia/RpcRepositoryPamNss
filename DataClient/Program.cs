using System;
namespace RpcScandinavia.Repository.PamNss.Internal;

/// <summary>
/// Simple program that can perform gRPC requests for the RPC Repository NSS module, when the owner process is forked.
/// Only valid return are written to StdOut as JSON, everything else is written to StdErr. Exit Code 0 indicates success.
/// </summary>
internal static class Program {

    public static Int32 Main(String[] args) {

		AppDomain.CurrentDomain.FirstChanceException += (sender, eventArgs) => {
			// Write error.
			//Console.Error.WriteLine($"End process: {Environment.ProcessId} (FirstChanceException): {eventArgs.Exception.Message}");
		}; // FirstChanceException

		try {
			// Validate.
			if ((args.Length < 1) || (args.Length > 2)) {
				// Write error.
				throw new ArgumentException($"Input must be: <request name>  [match]");
			}

			// Load configuration.
			Configuration options = new Configuration();

			// Write info.
			Console.Error.WriteLine($"Begin process: {Environment.ProcessId}. Configuration: '{options.Host}:{options.Port}'. Arguments: {String.Join(" ", args)}");

			// Initialise gRPC.
			using DataClient client = new DataClient(options);

			// Process request.
			String reply = null;
			switch (args[0]) {
				case "GetAllUsers": {
					reply = DataSerializer.Default.Serialize<NssUserDto[]>(client.GetAllUsers());
				} break;

				case "GetUserById": {
					if (UInt32.TryParse(args[1], out UInt32 userId) == false) {
						throw new ArgumentException($"Input must be: {args[0]}  <userId>");
					}

					reply = DataSerializer.Default.Serialize<NssUserDto>(client.GetUserById(userId));
				} break;

				case "GetUserByName": {
					String userName = args[1];
					if (String.IsNullOrWhiteSpace(userName) == true) {
						throw new ArgumentException($"Input must be: {args[0]}  <userName>");
					}

					reply = DataSerializer.Default.Serialize<NssUserDto>(client.GetUserByName(userName));
				} break;

				case "GetUserMembershipsByName": {
					String userName = args[1];
					if (String.IsNullOrWhiteSpace(userName) == true) {
						throw new ArgumentException($"Input must be: {args[0]}  <userName>");
					}

					reply = DataSerializer.Default.Serialize<UInt32[]>(client.GetUserMembershipsByName(userName));
				} break;

				case "GetAllShadows": {
					reply = DataSerializer.Default.Serialize<NssShadowDto[]>(client.GetAllShadows());
				} break;

				case "GetShadowByName": {
					String userName = args[1];
					if (String.IsNullOrWhiteSpace(userName) == true) {
						throw new ArgumentException($"Input must be: {args[0]}  <userName>");
					}

					reply = DataSerializer.Default.Serialize<NssShadowDto>(client.GetShadowByName(userName));
				} break;

				case "GetAllGroups": {
					reply = DataSerializer.Default.Serialize<NssGroupDto[]>(client.GetAllGroups());
				} break;

				case "GetGroupById": {
					if (UInt32.TryParse(args[1], out UInt32 groupId) == false) {
						throw new ArgumentException($"Input must be: {args[0]}  <groupId>");
					}

					reply = DataSerializer.Default.Serialize<NssGroupDto>(client.GetGroupById(groupId));
				} break;

				case "GetGroupByName": {
					String groupName = args[1];
					if (String.IsNullOrWhiteSpace(groupName) == true) {
						throw new ArgumentException($"Input must be: {args[0]}  <groupName>");
					}

					reply = DataSerializer.Default.Serialize<NssGroupDto>(client.GetGroupByName(groupName));
				} break;

				default: {
					throw new ArgumentException($"Input request name is unknown: '{args[0]}'.");
				}
			}

			// Write info.
			Console.Error.WriteLine($"End process: {Environment.ProcessId}.");

			// Return the response.
			Console.Write(reply);
			return 0;
        } catch (Exception exception) {
			// Write error.
			Console.Error.WriteLine($"End process: {Environment.ProcessId}: {exception.Message}");
			return 1;
		}
    } // Main

} // Program
