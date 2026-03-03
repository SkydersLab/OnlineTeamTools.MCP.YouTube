using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnlineTeamTools.MCP.YouTube.Protocol;

public sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; set; }

    [JsonPropertyName("id")]
    public JsonElement Id { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("params")]
    public JsonElement Params { get; set; }
}
