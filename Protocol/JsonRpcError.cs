namespace OnlineTeamTools.MCP.YouTube.Protocol;

public sealed class JsonRpcError
{
    public const int ParseErrorCode = -32700;
    public const int InvalidRequestCode = -32600;
    public const int MethodNotFoundCode = -32601;
    public const int InvalidParamsCode = -32602;
    public const int InternalErrorCode = -32603;
    public const int ServerErrorCode = -32000;

    public int Code { get; init; }

    public string Message { get; init; } = string.Empty;

    public object? Data { get; init; }

    public static JsonRpcError ParseError(string message, object? data = null)
        => new() { Code = ParseErrorCode, Message = message, Data = data };

    public static JsonRpcError InvalidRequest(string message, object? data = null)
        => new() { Code = InvalidRequestCode, Message = message, Data = data };

    public static JsonRpcError MethodNotFound(string message, object? data = null)
        => new() { Code = MethodNotFoundCode, Message = message, Data = data };

    public static JsonRpcError InvalidParams(string message, object? data = null)
        => new() { Code = InvalidParamsCode, Message = message, Data = data };

    public static JsonRpcError InternalError(string message, object? data = null)
        => new() { Code = InternalErrorCode, Message = message, Data = data };

    public static JsonRpcError ServerError(string message, object? data = null)
        => new() { Code = ServerErrorCode, Message = message, Data = data };
}
