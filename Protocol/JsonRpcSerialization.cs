using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnlineTeamTools.MCP.YouTube.Protocol;

public static class JsonRpcSerialization
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
