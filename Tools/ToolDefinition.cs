using System.Text.Json.Nodes;

namespace OnlineTeamTools.MCP.YouTube.Tools;

public sealed class ToolDefinition
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public JsonNode InputSchema { get; init; } = new JsonObject();
}
