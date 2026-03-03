using System.Globalization;
using System.Text.Json;
using OnlineTeamTools.MCP.YouTube.Infrastructure;
using OnlineTeamTools.MCP.YouTube.Jobs;
using OnlineTeamTools.MCP.YouTube.Protocol;
using OnlineTeamTools.MCP.YouTube.YouTube;

namespace OnlineTeamTools.MCP.YouTube.Tools;

public sealed class ToolDispatcher
{
    private const string ToolsListMethod = "tools/list";
    private const string ToolsCallMethod = "tools/call";

    private static readonly JsonElement EmptyArguments = JsonDocument.Parse("{}").RootElement.Clone();

    private readonly Dictionary<string, ToolEntry> _tools;
    private readonly StderrLogger _logger;
    private readonly YouTubeOptions _options;
    private readonly YouTubeUploader _uploader;
    private readonly YouTubeVideoReader _videoReader;
    private readonly UploadJobManager _jobManager;

    public ToolDispatcher(
        StderrLogger logger,
        YouTubeOptions options,
        YouTubeUploader uploader,
        YouTubeVideoReader videoReader,
        UploadJobManager jobManager)
    {
        _logger = logger;
        _options = options;
        _uploader = uploader;
        _videoReader = videoReader;
        _jobManager = jobManager;

        _tools = new Dictionary<string, ToolEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["youtube.upload_video"] = new ToolEntry(
                new ToolDefinition
                {
                    Name = "youtube.upload_video",
                    Description = "Upload a video file to YouTube (resumable upload). Defaults to privacy=private.",
                    InputSchema = ToolSchemas.UploadVideo
                },
                UploadVideoAsync),
            ["youtube.upload_thumbnail"] = new ToolEntry(
                new ToolDefinition
                {
                    Name = "youtube.upload_thumbnail",
                    Description = "Upload/set a thumbnail for an existing video.",
                    InputSchema = ToolSchemas.UploadThumbnail
                },
                UploadThumbnailAsync),
            ["youtube.update_metadata"] = new ToolEntry(
                new ToolDefinition
                {
                    Name = "youtube.update_metadata",
                    Description = "Update title/description/privacy/tags/etc for a video.",
                    InputSchema = ToolSchemas.UpdateMetadata
                },
                UpdateMetadataAsync),
            ["youtube.get_video"] = new ToolEntry(
                new ToolDefinition
                {
                    Name = "youtube.get_video",
                    Description = "Get basic details/status for a video (processing/ready/failed if possible).",
                    InputSchema = ToolSchemas.GetVideo
                },
                GetVideoAsync),
            ["youtube.create_upload_job"] = new ToolEntry(
                new ToolDefinition
                {
                    Name = "youtube.create_upload_job",
                    Description = "Start an upload asynchronously; return job_id immediately.",
                    InputSchema = ToolSchemas.UploadVideo
                },
                CreateUploadJobAsync),
            ["youtube.get_job"] = new ToolEntry(
                new ToolDefinition
                {
                    Name = "youtube.get_job",
                    Description = "Get upload job status and progress.",
                    InputSchema = ToolSchemas.GetJob
                },
                GetJobAsync),
            ["youtube.cancel_job"] = new ToolEntry(
                new ToolDefinition
                {
                    Name = "youtube.cancel_job",
                    Description = "Cancel an upload job.",
                    InputSchema = ToolSchemas.CancelJob
                },
                CancelJobAsync)
        };
    }

    public async Task<JsonRpcResponse> DispatchAsync(JsonElement requestElement, CancellationToken serverCancellationToken)
    {
        if (requestElement.ValueKind is not JsonValueKind.Object)
        {
            return JsonRpcResponse.FromError(null, JsonRpcError.InvalidRequest("JSON-RPC request must be a JSON object."));
        }

        JsonRpcRequest? request;

        try
        {
            request = JsonSerializer.Deserialize<JsonRpcRequest>(requestElement.GetRawText(), JsonRpcSerialization.Options);
        }
        catch (JsonException)
        {
            return JsonRpcResponse.FromError(null, JsonRpcError.InvalidRequest("Invalid JSON-RPC request object."));
        }

        if (request is null)
        {
            return JsonRpcResponse.FromError(null, JsonRpcError.InvalidRequest("Request payload is empty."));
        }

        if (!string.Equals(request.JsonRpc, "2.0", StringComparison.Ordinal))
        {
            return JsonRpcResponse.FromError(null, JsonRpcError.InvalidRequest("Only JSON-RPC version 2.0 is supported."));
        }

        if (!JsonRpcId.TryNormalize(request.Id, out var requestId))
        {
            return JsonRpcResponse.FromError(null, JsonRpcError.InvalidRequest("Invalid request id type. Use string, number, or null."));
        }

        if (string.IsNullOrWhiteSpace(request.Method))
        {
            return JsonRpcResponse.FromError(requestId, JsonRpcError.InvalidRequest("Missing required method."));
        }

        _logger.Info("Received request", ("id", requestId), ("method", request.Method));

        return request.Method switch
        {
            ToolsListMethod => JsonRpcResponse.FromResult(requestId, new { tools = ListTools() }),
            ToolsCallMethod => await HandleToolsCallAsync(requestId, request.Params, serverCancellationToken).ConfigureAwait(false),
            _ => JsonRpcResponse.FromError(requestId, JsonRpcError.MethodNotFound($"Method '{request.Method}' is not supported."))
        };
    }

    private IReadOnlyList<ToolDefinition> ListTools()
    {
        return _tools.Values
            .Select(entry => entry.Definition)
            .OrderBy(definition => definition.Name, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<JsonRpcResponse> HandleToolsCallAsync(object? requestId, JsonElement requestParams, CancellationToken serverCancellationToken)
    {
        if (requestParams.ValueKind is not JsonValueKind.Object)
        {
            return JsonRpcResponse.FromError(requestId, JsonRpcError.InvalidParams("tools/call expects an object params payload."));
        }

        if (!requestParams.TryGetProperty("name", out var nameProperty) || nameProperty.ValueKind is not JsonValueKind.String)
        {
            return JsonRpcResponse.FromError(requestId, JsonRpcError.InvalidParams("Missing required string param: name."));
        }

        var toolName = nameProperty.GetString();
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return JsonRpcResponse.FromError(requestId, JsonRpcError.InvalidParams("Tool name cannot be empty."));
        }

        if (!_tools.TryGetValue(toolName, out var entry))
        {
            return JsonRpcResponse.FromError(requestId, JsonRpcError.InvalidParams($"Tool '{toolName}' was not found."));
        }

        var arguments = EmptyArguments;
        if (requestParams.TryGetProperty("arguments", out var argumentProperty))
        {
            if (argumentProperty.ValueKind is not JsonValueKind.Object)
            {
                return JsonRpcResponse.FromError(requestId, JsonRpcError.InvalidParams("arguments must be an object."));
            }

            arguments = argumentProperty;
        }

        _logger.Info("Calling tool", ("id", requestId), ("tool", toolName));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(serverCancellationToken);
        timeoutCts.CancelAfter(_options.ToolTimeout);

        try
        {
            var data = await entry.Handler(arguments, requestId, timeoutCts.Token).ConfigureAwait(false);
            return JsonRpcResponse.FromResult(requestId, new { data });
        }
        catch (OperationCanceledException) when (!serverCancellationToken.IsCancellationRequested)
        {
            return JsonRpcResponse.FromError(requestId, JsonRpcError.ServerError("Tool call timed out."));
        }
        catch (ToolExecutionException ex)
        {
            return JsonRpcResponse.FromError(requestId, new JsonRpcError
            {
                Code = ex.Code,
                Message = ex.Message,
                Data = ex.DataObject
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Tool execution failed", ("id", requestId), ("tool", toolName), ("error", ex.Message));
            return JsonRpcResponse.FromError(requestId, JsonRpcError.InternalError("Tool execution failed."));
        }
    }

    private async Task<object> UploadVideoAsync(JsonElement arguments, object? requestId, CancellationToken cancellationToken)
    {
        var command = ParseUploadVideo(arguments);
        var result = await _uploader.UploadVideoAsync(command, null, cancellationToken).ConfigureAwait(false);

        _logger.Info("Upload completed", ("id", requestId), ("video_id", result.VideoId));
        return new
        {
            video_id = result.VideoId,
            url = result.Url
        };
    }

    private async Task<object> UploadThumbnailAsync(JsonElement arguments, object? requestId, CancellationToken cancellationToken)
    {
        var videoId = GetRequiredString(arguments, "video_id", allowedProperties: new HashSet<string>(StringComparer.Ordinal)
        {
            "video_id",
            "image_path"
        });

        var imagePath = GetRequiredString(arguments, "image_path");

        await _uploader.UploadThumbnailAsync(new UploadThumbnailCommand(videoId, imagePath), cancellationToken).ConfigureAwait(false);

        _logger.Info("Thumbnail uploaded", ("id", requestId), ("video_id", videoId));
        return new { ok = true };
    }

    private async Task<object> UpdateMetadataAsync(JsonElement arguments, object? requestId, CancellationToken cancellationToken)
    {
        EnsureAllowedProperties(arguments, new HashSet<string>(StringComparer.Ordinal)
        {
            "video_id",
            "title",
            "description",
            "privacy",
            "tags",
            "category_id",
            "made_for_kids"
        });

        var command = new UpdateMetadataCommand(
            VideoId: GetRequiredString(arguments, "video_id"),
            Title: GetOptionalString(arguments, "title"),
            Description: GetOptionalString(arguments, "description"),
            Privacy: GetOptionalPrivacy(arguments, "privacy"),
            Tags: GetOptionalStringArray(arguments, "tags"),
            CategoryId: GetOptionalString(arguments, "category_id"),
            MadeForKids: GetOptionalBoolean(arguments, "made_for_kids"));

        await _uploader.UpdateMetadataAsync(command, cancellationToken).ConfigureAwait(false);

        _logger.Info("Metadata updated", ("id", requestId), ("video_id", command.VideoId));
        return new { ok = true };
    }

    private async Task<object> GetVideoAsync(JsonElement arguments, object? requestId, CancellationToken cancellationToken)
    {
        var videoId = GetRequiredString(arguments, "video_id", allowedProperties: new HashSet<string>(StringComparer.Ordinal) { "video_id" });
        var result = await _videoReader.GetVideoAsync(videoId, cancellationToken).ConfigureAwait(false);

        _logger.Info("Video fetched", ("id", requestId), ("video_id", result.VideoId));
        return new
        {
            video_id = result.VideoId,
            status = result.Status,
            privacy = result.Privacy,
            title = result.Title,
            url = result.Url
        };
    }

    private Task<object> CreateUploadJobAsync(JsonElement arguments, object? requestId, CancellationToken cancellationToken)
    {
        var command = ParseUploadVideo(arguments);
        _uploader.ValidateUploadCommand(command);
        var jobId = _jobManager.CreateUploadJob(command, requestId, cancellationToken);

        _logger.Info("Upload job queued", ("id", requestId), ("job_id", jobId));
        return Task.FromResult<object>(new { job_id = jobId });
    }

    private Task<object> GetJobAsync(JsonElement arguments, object? requestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var jobId = GetRequiredString(arguments, "job_id", allowedProperties: new HashSet<string>(StringComparer.Ordinal) { "job_id" });
        var snapshot = _jobManager.GetJob(jobId)
            ?? throw new ToolExecutionException("Job was not found.", JsonRpcError.InvalidParamsCode, new { job_id = jobId });

        _logger.Info("Job fetched", ("id", requestId), ("job_id", jobId), ("state", snapshot.State));
        return Task.FromResult<object>(new
        {
            job_id = snapshot.JobId,
            state = snapshot.State,
            progress = snapshot.Progress,
            video_id = snapshot.VideoId,
            error = snapshot.Error
        });
    }

    private Task<object> CancelJobAsync(JsonElement arguments, object? requestId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var jobId = GetRequiredString(arguments, "job_id", allowedProperties: new HashSet<string>(StringComparer.Ordinal) { "job_id" });

        if (!_jobManager.CancelJob(jobId))
        {
            throw new ToolExecutionException("Job was not found.", JsonRpcError.InvalidParamsCode, new { job_id = jobId });
        }

        _logger.Info("Job canceled", ("id", requestId), ("job_id", jobId));
        return Task.FromResult<object>(new { ok = true });
    }

    private UploadVideoCommand ParseUploadVideo(JsonElement arguments)
    {
        EnsureAllowedProperties(arguments, new HashSet<string>(StringComparer.Ordinal)
        {
            "file_path",
            "title",
            "description",
            "privacy",
            "tags",
            "category_id",
            "made_for_kids",
            "publish_at"
        });

        var isPrivacyExplicit = arguments.TryGetProperty("privacy", out var privacyProperty);

        var privacy = isPrivacyExplicit
            ? ParsePrivacy(privacyProperty)
            : ResolveDefaultPrivacy();

        var publishAt = GetOptionalDateTimeOffset(arguments, "publish_at");
        if (publishAt.HasValue && !string.Equals(privacy, "private", StringComparison.Ordinal))
        {
            throw new ToolExecutionException("publish_at requires privacy=private.", JsonRpcError.InvalidParamsCode);
        }

        return new UploadVideoCommand(
            FilePath: GetRequiredString(arguments, "file_path"),
            Title: GetRequiredString(arguments, "title"),
            Description: GetRequiredString(arguments, "description"),
            Privacy: privacy,
            Tags: GetOptionalStringArray(arguments, "tags") ?? Array.Empty<string>(),
            CategoryId: GetOptionalString(arguments, "category_id"),
            MadeForKids: GetOptionalBoolean(arguments, "made_for_kids") ?? false,
            PublishAt: publishAt,
            IsPrivacyExplicit: isPrivacyExplicit);
    }

    private string ResolveDefaultPrivacy()
    {
        _ = _options.DefaultPrivacy;
        return "private";
    }

    private static string ParsePrivacy(JsonElement element)
    {
        if (element.ValueKind is not JsonValueKind.String)
        {
            throw new ToolExecutionException("privacy must be a string.", JsonRpcError.InvalidParamsCode);
        }

        var value = element.GetString()?.Trim().ToLowerInvariant();
        if (value is not ("private" or "unlisted" or "public"))
        {
            throw new ToolExecutionException("privacy must be one of: private, unlisted, public.", JsonRpcError.InvalidParamsCode);
        }

        return value;
    }

    private static void EnsureAllowedProperties(JsonElement arguments, IReadOnlySet<string> allowed)
    {
        if (arguments.ValueKind is not JsonValueKind.Object)
        {
            throw new ToolExecutionException("Tool arguments must be a JSON object.", JsonRpcError.InvalidParamsCode);
        }

        foreach (var property in arguments.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw new ToolExecutionException(
                    $"Unknown argument '{property.Name}'.",
                    JsonRpcError.InvalidParamsCode,
                    new { argument = property.Name });
            }
        }
    }

    private static string GetRequiredString(JsonElement arguments, string propertyName, IReadOnlySet<string>? allowedProperties = null)
    {
        if (allowedProperties is not null)
        {
            EnsureAllowedProperties(arguments, allowedProperties);
        }

        if (!arguments.TryGetProperty(propertyName, out var property) || property.ValueKind is not JsonValueKind.String)
        {
            throw new ToolExecutionException($"Missing required string argument '{propertyName}'.", JsonRpcError.InvalidParamsCode);
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ToolExecutionException($"Argument '{propertyName}' cannot be empty.", JsonRpcError.InvalidParamsCode);
        }

        return value;
    }

    private static string? GetOptionalString(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind is not JsonValueKind.String)
        {
            throw new ToolExecutionException($"Argument '{propertyName}' must be a string.", JsonRpcError.InvalidParamsCode);
        }

        return property.GetString();
    }

    private static bool? GetOptionalBoolean(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind is JsonValueKind.True)
        {
            return true;
        }

        if (property.ValueKind is JsonValueKind.False)
        {
            return false;
        }

        throw new ToolExecutionException($"Argument '{propertyName}' must be a boolean.", JsonRpcError.InvalidParamsCode);
    }

    private static IReadOnlyList<string>? GetOptionalStringArray(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind is not JsonValueKind.Array)
        {
            throw new ToolExecutionException($"Argument '{propertyName}' must be an array of strings.", JsonRpcError.InvalidParamsCode);
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind is not JsonValueKind.String)
            {
                throw new ToolExecutionException($"Argument '{propertyName}' must contain only strings.", JsonRpcError.InvalidParamsCode);
            }

            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static string? GetOptionalPrivacy(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        return ParsePrivacy(property);
    }

    private static DateTimeOffset? GetOptionalDateTimeOffset(JsonElement arguments, string propertyName)
    {
        if (!arguments.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind is not JsonValueKind.String)
        {
            throw new ToolExecutionException($"Argument '{propertyName}' must be an ISO-8601 datetime string.", JsonRpcError.InvalidParamsCode);
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            throw new ToolExecutionException($"Argument '{propertyName}' must be a valid ISO-8601 datetime.", JsonRpcError.InvalidParamsCode);
        }

        return parsed;
    }

    private sealed record ToolEntry(ToolDefinition Definition, Func<JsonElement, object?, CancellationToken, Task<object>> Handler);
}
