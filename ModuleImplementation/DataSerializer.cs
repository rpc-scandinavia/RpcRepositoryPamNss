using System;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace RpcScandinavia.Repository.PamNss;

[JsonSourceGenerationOptions(WriteIndented = true, IncludeFields = true)]
[JsonSerializable(typeof(NssUserDto))]
[JsonSerializable(typeof(NssUserDto[]))]
[JsonSerializable(typeof(NssShadowDto))]
[JsonSerializable(typeof(NssShadowDto[]))]
[JsonSerializable(typeof(NssGroupDto))]
[JsonSerializable(typeof(UInt32[]))]
[JsonSerializable(typeof(NssGroupDto[]))]public partial class DataSerializer : JsonSerializerContext {

	public T Deserialize<T>(String text) where T: class {
		return (JsonSerializer.Deserialize(text, typeof(T), DataSerializer.Default) as T);
	} // Deserialize

	public String Serialize<T>(T obj) {
		return JsonSerializer.Serialize(obj, typeof(T), DataSerializer.Default);
	} // Serialize

} // DataSerializer
