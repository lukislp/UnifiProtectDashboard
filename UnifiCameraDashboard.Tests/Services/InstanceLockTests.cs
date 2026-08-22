using Microsoft.Extensions.Logging.Abstractions;
using UnifiCameraDashboard.BackgroundServices;
using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Tests.Services;

public class InstanceLockTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), "unifi-instance-lock-tests-" + Guid.NewGuid().ToString("N"));

    public InstanceLockTests()
    {
        Directory.CreateDirectory(_dataDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDir))
        {
            Directory.Delete(_dataDir, recursive: true);
        }
    }

    [Fact]
    public async Task WhenAcquiredAsync_NoContention_CompletesImmediately()
    {
        await using var instanceLock = new FileInstanceLock(new DataDirectoryOptions(_dataDir), NullLogger<FileInstanceLock>.Instance);

        var task = instanceLock.WhenAcquiredAsync(CancellationToken.None);
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(task, completed);
    }

    [Fact]
    public async Task WhenAcquiredAsync_SecondInstanceBlocksUntilFirstReleases()
    {
        await using var first = new FileInstanceLock(new DataDirectoryOptions(_dataDir), NullLogger<FileInstanceLock>.Instance);
        var second = new FileInstanceLock(new DataDirectoryOptions(_dataDir), NullLogger<FileInstanceLock>.Instance);

        await first.WhenAcquiredAsync(CancellationToken.None);

        // Simulates the new pod waiting for the old pod (holding "first") to actually stop -
        // must still be pending shortly after, not resolved immediately.
        var secondTask = second.WhenAcquiredAsync(CancellationToken.None);
        var stillWaiting = await Task.WhenAny(secondTask, Task.Delay(TimeSpan.FromMilliseconds(500)));
        Assert.NotSame(secondTask, stillWaiting);

        // Old pod stops - releases the OS-level file lock.
        await first.DisposeAsync();

        var completed = await Task.WhenAny(secondTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(secondTask, completed);

        await second.DisposeAsync();
    }

    [Fact]
    public async Task WhenAcquiredAsync_CalledConcurrentlyOnSameInstance_BothResolveWithoutDeadlock()
    {
        await using var instanceLock = new FileInstanceLock(new DataDirectoryOptions(_dataDir), NullLogger<FileInstanceLock>.Instance);

        // Simulates EventIngestionService and EventClassificationService both awaiting the
        // same singleton lock instance at startup.
        var first = instanceLock.WhenAcquiredAsync(CancellationToken.None);
        var second = instanceLock.WhenAcquiredAsync(CancellationToken.None);

        var all = Task.WhenAll(first, second);
        var completed = await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(all, completed);
    }
}
