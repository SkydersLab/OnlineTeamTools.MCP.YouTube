namespace OnlineTeamTools.MCP.YouTube.Jobs;

internal sealed class UploadJob
{
    private readonly object _sync = new();

    public UploadJob(string jobId, CancellationTokenSource cancellation)
    {
        JobId = jobId;
        Cancellation = cancellation;
        State = "queued";
        Progress = 0;
    }

    public string JobId { get; }

    public CancellationTokenSource Cancellation { get; }

    private string State { get; set; }

    private double Progress { get; set; }

    private string? VideoId { get; set; }

    private string? Error { get; set; }

    public void MarkRunning()
    {
        lock (_sync)
        {
            if (State == "queued")
            {
                State = "running";
            }
        }
    }

    public void SetProgress(double value)
    {
        lock (_sync)
        {
            if (State == "queued")
            {
                State = "running";
            }

            Progress = Math.Clamp(value, 0, 100);
        }
    }

    public void MarkCompleted(string videoId)
    {
        lock (_sync)
        {
            State = "completed";
            Progress = 100;
            VideoId = videoId;
            Error = null;
        }
    }

    public void MarkFailed(string message)
    {
        lock (_sync)
        {
            State = "failed";
            Error = message;
        }
    }

    public void MarkCanceled()
    {
        lock (_sync)
        {
            State = "canceled";
            Error = null;
        }
    }

    public UploadJobSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new UploadJobSnapshot(JobId, State, Progress, VideoId, Error);
        }
    }
}

public sealed record UploadJobSnapshot(string JobId, string State, double Progress, string? VideoId, string? Error);
