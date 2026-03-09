namespace OnlineTeamTools.MCP.YouTube.YouTube;

public sealed record UploadVideoCommand(
    string FilePath,
    string Title,
    string Description,
    string Privacy,
    IReadOnlyList<string> Tags,
    string? CategoryId,
    bool MadeForKids,
    DateTimeOffset? PublishAt,
    bool IsPrivacyExplicit);

public sealed record UploadVideoResult(string VideoId, string Url);

public sealed record UploadThumbnailCommand(string VideoId, string ImagePath);

public sealed record UpdateMetadataCommand(
    string VideoId,
    string? Title,
    string? Description,
    string? Privacy,
    IReadOnlyList<string>? Tags,
    string? CategoryId,
    bool? MadeForKids);

public sealed record VideoInfoResult(
    string VideoId,
    string Status,
    string Privacy,
    string Title,
    string Url);

public sealed record CreatePlaylistCommand(
    string Title,
    string? Description,
    string Privacy);

public sealed record CreatePlaylistResult(
    string PlaylistId,
    string Title,
    string Privacy,
    string Url);

public sealed record AddVideosToPlaylistCommand(
    string PlaylistId,
    IReadOnlyList<string> VideoIds,
    int? Position);

public sealed record AddedPlaylistItemResult(
    string VideoId,
    string PlaylistItemId,
    long? Position);

public sealed record AddVideosToPlaylistResult(
    string PlaylistId,
    IReadOnlyList<AddedPlaylistItemResult> ItemsAdded);

public sealed record PlaylistInfoResult(
    string PlaylistId,
    string Title,
    string Privacy,
    long? ItemCount,
    string Url);
