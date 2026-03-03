using Google.Apis.Upload;
using Google.Apis.YouTube.v3.Data;
using OnlineTeamTools.MCP.YouTube.Tools;

namespace OnlineTeamTools.MCP.YouTube.YouTube;

public sealed class YouTubeUploader
{
    private readonly YouTubeClientFactory _clientFactory;
    private readonly PathSafetyValidator _pathSafetyValidator;
    private readonly UploadConcurrencyGate _concurrencyGate;

    public YouTubeUploader(
        YouTubeClientFactory clientFactory,
        PathSafetyValidator pathSafetyValidator,
        UploadConcurrencyGate concurrencyGate)
    {
        _clientFactory = clientFactory;
        _pathSafetyValidator = pathSafetyValidator;
        _concurrencyGate = concurrencyGate;
    }

    public void ValidateUploadCommand(UploadVideoCommand command)
    {
        _pathSafetyValidator.ValidateVideoPath(command.FilePath);
    }

    public async Task<UploadVideoResult> UploadVideoAsync(
        UploadVideoCommand command,
        Action<double>? progressCallback,
        CancellationToken cancellationToken)
    {
        var videoPath = _pathSafetyValidator.ValidateVideoPath(command.FilePath);

        await using var gate = await _concurrencyGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
        using var service = await _clientFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        await using var videoStream = new FileStream(videoPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var snippet = new VideoSnippet
        {
            Title = command.Title,
            Description = command.Description,
            CategoryId = command.CategoryId
        };

        if (command.Tags.Count > 0)
        {
            snippet.Tags = command.Tags.ToList();
        }

        var status = new VideoStatus
        {
            PrivacyStatus = command.Privacy,
            SelfDeclaredMadeForKids = command.MadeForKids
        };

        if (command.PublishAt.HasValue)
        {
            status.PublishAtDateTimeOffset = command.PublishAt;
        }

        var video = new Video
        {
            Snippet = snippet,
            Status = status
        };

        var uploadRequest = service.Videos.Insert(video, "snippet,status", videoStream, GetVideoMimeType(videoPath));
        uploadRequest.ChunkSize = ResumableUpload.MinimumChunkSize * 8;

        string? uploadedVideoId = null;

        uploadRequest.ProgressChanged += progress =>
        {
            if (progressCallback is null)
            {
                return;
            }

            var percent = videoStream.Length <= 0
                ? 0
                : Math.Clamp(progress.BytesSent * 100d / videoStream.Length, 0, 100);

            progressCallback(Math.Round(percent, 2));
        };

        uploadRequest.ResponseReceived += uploadedVideo =>
        {
            uploadedVideoId = uploadedVideo?.Id;
        };

        var uploadProgress = await uploadRequest.UploadAsync(cancellationToken).ConfigureAwait(false);
        if (uploadProgress.Status == UploadStatus.Completed)
        {
            progressCallback?.Invoke(100);

            uploadedVideoId ??= uploadRequest.ResponseBody?.Id;
            if (string.IsNullOrWhiteSpace(uploadedVideoId))
            {
                throw new ToolExecutionException("Upload completed but video id was not returned.");
            }

            return new UploadVideoResult(uploadedVideoId, $"https://youtu.be/{uploadedVideoId}");
        }

        if (uploadProgress.Status == UploadStatus.Failed)
        {
            throw new ToolExecutionException(
                $"Upload failed: {uploadProgress.Exception?.Message ?? "Unknown upload error."}",
                Protocol.JsonRpcError.ServerErrorCode);
        }

        throw new ToolExecutionException($"Upload did not complete successfully. Status: {uploadProgress.Status}");
    }

    public async Task UploadThumbnailAsync(UploadThumbnailCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.VideoId))
        {
            throw new ToolExecutionException("video_id is required.", Protocol.JsonRpcError.InvalidParamsCode);
        }

        var imagePath = _pathSafetyValidator.ValidateImagePath(command.ImagePath);

        using var service = await _clientFactory.CreateAsync(cancellationToken).ConfigureAwait(false);
        await using var imageStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var request = service.Thumbnails.Set(command.VideoId, imageStream, GetImageMimeType(imagePath));
        var progress = await request.UploadAsync(cancellationToken).ConfigureAwait(false);

        if (progress.Status == UploadStatus.Failed)
        {
            throw new ToolExecutionException(
                $"Thumbnail upload failed: {progress.Exception?.Message ?? "Unknown upload error."}",
                Protocol.JsonRpcError.ServerErrorCode);
        }

        if (progress.Status != UploadStatus.Completed)
        {
            throw new ToolExecutionException($"Thumbnail upload did not complete successfully. Status: {progress.Status}");
        }
    }

    public async Task UpdateMetadataAsync(UpdateMetadataCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.VideoId))
        {
            throw new ToolExecutionException("video_id is required.", Protocol.JsonRpcError.InvalidParamsCode);
        }

        using var service = await _clientFactory.CreateAsync(cancellationToken).ConfigureAwait(false);

        var listRequest = service.Videos.List("snippet,status");
        listRequest.Id = command.VideoId;

        var listResponse = await listRequest.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        var existing = listResponse.Items?.FirstOrDefault();
        if (existing is null)
        {
            throw new ToolExecutionException("Video not found.", Protocol.JsonRpcError.ServerErrorCode, new { video_id = command.VideoId });
        }

        var snippet = existing.Snippet ?? new VideoSnippet();
        var status = existing.Status ?? new VideoStatus();

        if (command.Title is not null)
        {
            snippet.Title = command.Title;
        }

        if (command.Description is not null)
        {
            snippet.Description = command.Description;
        }

        if (command.Tags is not null)
        {
            snippet.Tags = command.Tags.ToList();
        }

        if (command.CategoryId is not null)
        {
            snippet.CategoryId = command.CategoryId;
        }

        if (command.Privacy is not null)
        {
            status.PrivacyStatus = command.Privacy;
        }

        if (command.MadeForKids.HasValue)
        {
            status.SelfDeclaredMadeForKids = command.MadeForKids.Value;
        }

        var updateRequest = service.Videos.Update(new Video
        {
            Id = command.VideoId,
            Snippet = snippet,
            Status = status
        }, "snippet,status");

        await updateRequest.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string GetVideoMimeType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mov" => "video/quicktime",
            _ => "video/mp4"
        };
    }

    private static string GetImageMimeType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpeg" => "image/jpeg",
            ".jpg" => "image/jpeg",
            _ => "application/octet-stream"
        };
    }
}
