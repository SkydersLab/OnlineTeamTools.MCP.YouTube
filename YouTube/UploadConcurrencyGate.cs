namespace OnlineTeamTools.MCP.YouTube.YouTube;

public sealed class UploadConcurrencyGate
{
    private readonly SemaphoreSlim _semaphore;

    public UploadConcurrencyGate(YouTubeOptions options)
    {
        _semaphore = new SemaphoreSlim(options.Concurrency, options.Concurrency);
    }

    public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(_semaphore);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _released;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                _semaphore.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
