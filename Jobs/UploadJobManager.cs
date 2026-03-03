using System.Collections.Concurrent;
using OnlineTeamTools.MCP.YouTube.Infrastructure;
using OnlineTeamTools.MCP.YouTube.YouTube;

namespace OnlineTeamTools.MCP.YouTube.Jobs;

public sealed class UploadJobManager
{
    private readonly ConcurrentDictionary<string, UploadJob> _jobs = new(StringComparer.Ordinal);
    private readonly YouTubeUploader _uploader;
    private readonly StderrLogger _logger;

    public UploadJobManager(YouTubeUploader uploader, StderrLogger logger)
    {
        _uploader = uploader;
        _logger = logger;
    }

    public string CreateUploadJob(UploadVideoCommand command, object? requestId, CancellationToken serverCancellationToken)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var cts = CancellationTokenSource.CreateLinkedTokenSource(serverCancellationToken);

        var job = new UploadJob(jobId, cts);
        if (!_jobs.TryAdd(jobId, job))
        {
            throw new InvalidOperationException("Unable to create upload job.");
        }

        _ = Task.Run(() => RunJobAsync(job, command, requestId, cts.Token), CancellationToken.None);

        _logger.Info("Upload job created", ("request_id", requestId), ("job_id", jobId));
        return jobId;
    }

    public UploadJobSnapshot? GetJob(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var job)
            ? job.Snapshot()
            : null;
    }

    public bool CancelJob(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return false;
        }

        job.Cancellation.Cancel();
        return true;
    }

    private async Task RunJobAsync(UploadJob job, UploadVideoCommand command, object? requestId, CancellationToken cancellationToken)
    {
        job.MarkRunning();
        _logger.Info("Upload job started", ("request_id", requestId), ("job_id", job.JobId));

        try
        {
            var result = await _uploader.UploadVideoAsync(
                    command,
                    progress => job.SetProgress(progress),
                    cancellationToken)
                .ConfigureAwait(false);

            job.MarkCompleted(result.VideoId);
            _logger.Info("Upload job completed", ("job_id", job.JobId), ("video_id", result.VideoId));
        }
        catch (OperationCanceledException)
        {
            job.MarkCanceled();
            _logger.Warn("Upload job canceled", ("job_id", job.JobId));
        }
        catch (Exception ex)
        {
            job.MarkFailed(ex.Message);
            _logger.Error("Upload job failed", ("job_id", job.JobId), ("error", ex.Message));
        }
        finally
        {
            job.Cancellation.Dispose();
        }
    }
}
