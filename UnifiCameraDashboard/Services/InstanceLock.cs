using UnifiCameraDashboard.BackgroundServices;

namespace UnifiCameraDashboard.Services;

/// <summary>
/// Coordinates which pod instance is allowed to run the write-active background services
/// (event ingestion, classification) during a rolling update. The Deployment intentionally
/// allows a brief overlap (maxSurge: 1, maxUnavailable: 0, both pods pinned to the same node)
/// so the web UI stays reachable throughout a rollout - but only one instance may hold the
/// UniFi Protect websocket connection and advance the backfill/classification state at a time,
/// or both pods would race on the same watermark and double up on work. The old pod's copy of
/// this lock releases automatically when its process exits (the OS releases the file lock), so
/// the new pod's waiting acquire attempt unblocks the moment the old pod actually stops - not
/// on any fixed delay.
/// </summary>
public interface IInstanceLock
{
    Task WhenAcquiredAsync(CancellationToken cancellationToken);
}

public sealed class FileInstanceLock : IInstanceLock, IAsyncDisposable
{
    private readonly string _lockPath;
    private readonly ILogger<FileInstanceLock> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private FileStream? _stream;

    public FileInstanceLock(DataDirectoryOptions dataDirectory, ILogger<FileInstanceLock> logger)
    {
        _lockPath = Path.Combine(dataDirectory.Path, ".instance.lock");
        _logger = logger;
    }

    public async Task WhenAcquiredAsync(CancellationToken cancellationToken)
    {
        if (_stream != null)
        {
            return; // already acquired by an earlier caller in this process
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_stream != null)
            {
                return; // a concurrent caller in this process won the race while we waited
            }

            var delay = TimeSpan.FromSeconds(1);
            var maxDelay = TimeSpan.FromSeconds(10);
            var warned = false;
            while (true)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_lockPath)!);
                    _stream = new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    _logger.LogInformation("Acquired the write-instance lock");
                    return;
                }
                catch (IOException)
                {
                    if (!warned)
                    {
                        _logger.LogInformation("Waiting for the previous instance to release the write-instance lock (expected during a rolling update)...");
                        warned = true;
                    }
                    await Task.Delay(delay, cancellationToken);
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, maxDelay.TotalSeconds));
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream != null)
        {
            await _stream.DisposeAsync();
        }
    }
}
