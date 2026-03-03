using OnlineTeamTools.MCP.YouTube.Tools;

namespace OnlineTeamTools.MCP.YouTube.YouTube;

public sealed class YouTubeVideoReader
{
    private readonly YouTubeClientFactory _clientFactory;

    public YouTubeVideoReader(YouTubeClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<VideoInfoResult> GetVideoAsync(string videoId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(videoId))
        {
            throw new ToolExecutionException("video_id is required.", Protocol.JsonRpcError.InvalidParamsCode);
        }

        using var service = await _clientFactory.CreateAsync(cancellationToken).ConfigureAwait(false);

        var request = service.Videos.List("snippet,status,processingDetails");
        request.Id = videoId;

        var response = await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        var video = response.Items?.FirstOrDefault();

        if (video is null)
        {
            throw new ToolExecutionException("Video not found.", Protocol.JsonRpcError.ServerErrorCode, new { video_id = videoId });
        }

        var status = video.ProcessingDetails?.ProcessingStatus;
        if (string.IsNullOrWhiteSpace(status))
        {
            status = video.Status?.UploadStatus;
        }

        return new VideoInfoResult(
            VideoId: videoId,
            Status: status ?? "unknown",
            Privacy: video.Status?.PrivacyStatus ?? "unknown",
            Title: video.Snippet?.Title ?? string.Empty,
            Url: $"https://youtu.be/{videoId}");
    }
}
