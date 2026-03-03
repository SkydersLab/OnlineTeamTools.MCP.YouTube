namespace OnlineTeamTools.MCP.YouTube.Tools;

public sealed class ToolExecutionException : Exception
{
    public ToolExecutionException(string message, int code = Protocol.JsonRpcError.ServerErrorCode, object? data = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        DataObject = data;
    }

    public int Code { get; }

    public object? DataObject { get; }
}
