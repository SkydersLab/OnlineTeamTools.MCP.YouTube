using System.Text.Json.Nodes;

namespace OnlineTeamTools.MCP.YouTube.Tools;

public static class ToolSchemas
{
    public static JsonNode UploadVideo => Parse("""
    {
      "type":"object",
      "properties":{
        "file_path":{"type":"string","description":"Local path on the MCP machine. Must be under allowed root."},
        "title":{"type":"string"},
        "description":{"type":"string"},
        "privacy":{"type":"string","enum":["private","unlisted","public"],"default":"private"},
        "tags":{"type":"array","items":{"type":"string"}},
        "category_id":{"type":"string"},
        "made_for_kids":{"type":"boolean","default":false},
        "publish_at":{"type":"string","description":"Optional scheduled publish datetime (ISO-8601). If provided, set as scheduled if supported."}
      },
      "required":["file_path","title","description"],
      "additionalProperties":false
    }
    """);

    public static JsonNode UploadThumbnail => Parse("""
    {
      "type":"object",
      "properties":{
        "video_id":{"type":"string"},
        "image_path":{"type":"string","description":"Local path under allowed root."}
      },
      "required":["video_id","image_path"],
      "additionalProperties":false
    }
    """);

    public static JsonNode UpdateMetadata => Parse("""
    {
      "type":"object",
      "properties":{
        "video_id":{"type":"string"},
        "title":{"type":"string"},
        "description":{"type":"string"},
        "privacy":{"type":"string","enum":["private","unlisted","public"]},
        "tags":{"type":"array","items":{"type":"string"}},
        "category_id":{"type":"string"},
        "made_for_kids":{"type":"boolean"}
      },
      "required":["video_id"],
      "additionalProperties":false
    }
    """);

    public static JsonNode GetVideo => Parse("""
    {
      "type":"object",
      "properties":{"video_id":{"type":"string"}},
      "required":["video_id"],
      "additionalProperties":false
    }
    """);

    public static JsonNode GetJob => Parse("""
    {
      "type":"object",
      "properties":{"job_id":{"type":"string"}},
      "required":["job_id"],
      "additionalProperties":false
    }
    """);

    public static JsonNode CancelJob => Parse("""
    {
      "type":"object",
      "properties":{"job_id":{"type":"string"}},
      "required":["job_id"],
      "additionalProperties":false
    }
    """);

    private static JsonNode Parse(string json)
    {
        return JsonNode.Parse(json) ?? throw new InvalidOperationException("Failed to parse tool schema JSON.");
    }
}
