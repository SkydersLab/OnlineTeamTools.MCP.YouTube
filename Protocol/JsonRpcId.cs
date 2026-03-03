using System.Text.Json;

namespace OnlineTeamTools.MCP.YouTube.Protocol;

public static class JsonRpcId
{
    public static bool TryNormalize(JsonElement idElement, out object? normalized)
    {
        normalized = null;

        return idElement.ValueKind switch
        {
            JsonValueKind.Undefined => true,
            JsonValueKind.Null => true,
            JsonValueKind.String => Assign(idElement.GetString(), out normalized),
            JsonValueKind.Number when idElement.TryGetInt64(out var longValue) => Assign(longValue, out normalized),
            JsonValueKind.Number when idElement.TryGetDouble(out var doubleValue) => Assign(doubleValue, out normalized),
            _ => false
        };
    }

    private static bool Assign(object? value, out object? normalized)
    {
        normalized = value;
        return true;
    }
}
