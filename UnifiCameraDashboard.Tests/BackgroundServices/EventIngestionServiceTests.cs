using UnifiCameraDashboard.BackgroundServices;

namespace UnifiCameraDashboard.Tests.BackgroundServices;

public class EventIngestionServiceTests
{
    private static readonly TimeSpan Lookback = TimeSpan.FromHours(24);

    [Fact]
    public void ComputeBackfillStart_NoWatermark_StartsFromLookbackBound()
    {
        var now = new DateTimeOffset(2026, 8, 21, 20, 0, 0, TimeSpan.Zero);

        var start = EventIngestionService.ComputeBackfillStart(watermark: null, now, Lookback);

        Assert.Equal(now - Lookback, start);
    }

    [Fact]
    public void ComputeBackfillStart_RecentWatermark_ResumesFromWatermark()
    {
        var now = new DateTimeOffset(2026, 8, 21, 20, 0, 0, TimeSpan.Zero);
        var watermark = now.AddMinutes(-5);

        var start = EventIngestionService.ComputeBackfillStart(watermark, now, Lookback);

        Assert.Equal(watermark, start);
    }

    [Fact]
    public void ComputeBackfillStart_StaleWatermarkOlderThanLookback_ClampsToLookbackBound()
    {
        // Simulates a pod that was down for days: the last successful watermark is older than
        // the lookback window itself, so it should clamp rather than request an unbounded range.
        var now = new DateTimeOffset(2026, 8, 21, 20, 0, 0, TimeSpan.Zero);
        var staleWatermark = now - TimeSpan.FromDays(5);

        var start = EventIngestionService.ComputeBackfillStart(staleWatermark, now, Lookback);

        Assert.Equal(now - Lookback, start);
    }
}
