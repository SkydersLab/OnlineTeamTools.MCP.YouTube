namespace OnlineTeamTools.MCP.YouTube.Protocol;

public sealed class JsonRpcResponse
{
    public string Jsonrpc { get; init; } = "2.0";

    public object? Id { get; init; }

    public object? Result { get; init; }

    public JsonRpcError? Error { get; init; }

    public static JsonRpcResponse FromResult(object? id, object result)
        => new() { Id = id, Result = result };

    public static JsonRpcResponse FromError(object? id, JsonRpcError error)
        => new() { Id = id, Error = error };
}
