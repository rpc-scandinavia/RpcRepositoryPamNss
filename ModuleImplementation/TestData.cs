using System;
using System.Collections.Generic;
namespace RpcScandinavia.Repository.PamNss.Test;

public static class TestData {

	public static readonly Dictionary<String, String> Passwords = new Dictionary<String, String> {
		["tester"] = "password",
		["rpc@rpc-scandinavia.dk"] = "password",
	};

	public static readonly NssUserDto[] TestUsers = [
		new NssUserDto(
			5000,
			2000,
			"tester",
			"Test Testesen",
			"/home/test",
			"/bin/bash",
			DateTimeOffset.UtcNow.AddDays(7)
		),
		new NssUserDto(
			5001,
			2001,
			"rpc@rpc-scandinavia.dk",
			"René Paw Christensen",
			"/home/rpc@rpc-scandinavia.dk",
			"/bin/zsh",
			DateTimeOffset.UtcNow.AddDays(7)
		),
	];

	public static readonly NssShadowDto[] TestShadows = [
		new NssShadowDto(
			"tester",
			"*",
			DateTimeOffset.UtcNow.AddDays(7)
		),
		new NssShadowDto(
			"rpc@rpc-scandinavia.dk",
			"*",
			DateTimeOffset.UtcNow.AddDays(7)
		)
	];

	public static readonly NssGroupDto[] TestGroups = [
		new NssGroupDto(
			2000,
			"testers",
			["tester"]
		),
		new NssGroupDto(
			2001,
			"everyone@rpc-scandinavia.dk",
			["rpc@rpc-scandinavia.dk"]
		),
		new NssGroupDto(
			900,
			"itdd",
			["rpc@rpc-scandinavia.dk"]
		),
	];

} // TestData
