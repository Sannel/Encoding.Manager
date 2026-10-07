using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sannel.Encoding.Manager.DiscMenu.Model;

/// <summary>JSON settings shared by the probe (writer) and the web app (reader).</summary>
public static class DiscMenuJson
{
	public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
	{
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		Converters = { new JsonStringEnumConverter() },
	};

	public static string Serialize(DiscMenuMap map) => JsonSerializer.Serialize(map, Options);

	public static DiscMenuMap? Deserialize(string json) => JsonSerializer.Deserialize<DiscMenuMap>(json, Options);
}
