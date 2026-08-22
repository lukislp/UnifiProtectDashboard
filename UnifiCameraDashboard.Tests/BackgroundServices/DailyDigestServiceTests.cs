using UnifiCameraDashboard.BackgroundServices;
using UnifiCameraDashboard.Models;

namespace UnifiCameraDashboard.Tests.BackgroundServices;

public class DailyDigestServiceTests
{
    [Fact]
    public void FormatDigest_MultipleCamerasAndLabels_FormatsHighestCountFirst()
    {
        var cameras = new List<UnifiCamera>
        {
            new() { Id = "cam-1", Name = "Driveway" },
            new() { Id = "cam-2", Name = "Terrace" },
        };
        var counts = new List<(string CameraUnifiId, string Label, int Count)>
        {
            ("cam-1", "car", 1),
            ("cam-1", "person", 3),
        };

        var digest = DailyDigestService.FormatDigest(cameras, counts);

        Assert.Equal("Driveway: 3x person, 1x car; Terrace: quiet", digest);
    }

    [Fact]
    public void FormatDigest_CameraWithNoDetections_ShowsQuiet()
    {
        var cameras = new List<UnifiCamera> { new() { Id = "cam-1", Name = "Driveway" } };
        var counts = new List<(string CameraUnifiId, string Label, int Count)>();

        var digest = DailyDigestService.FormatDigest(cameras, counts);

        Assert.Equal("Driveway: quiet", digest);
    }

    [Fact]
    public void FormatDigest_NoCameras_ReturnsEmptyString()
    {
        var digest = DailyDigestService.FormatDigest(
            new List<UnifiCamera>(),
            new List<(string CameraUnifiId, string Label, int Count)>());

        Assert.Equal(string.Empty, digest);
    }

    [Fact]
    public void FormatDigest_CountsForARemovedCamera_AreIgnored()
    {
        // GetAllCamerasAsync is already Enabled-filtered, so a removed camera never appears in
        // the `cameras` list passed in here - its counts (if any leaked in) must not show up.
        var cameras = new List<UnifiCamera> { new() { Id = "cam-1", Name = "Driveway" } };
        var counts = new List<(string CameraUnifiId, string Label, int Count)>
        {
            ("cam-1", "person", 1),
            ("cam-removed", "person", 5),
        };

        var digest = DailyDigestService.FormatDigest(cameras, counts);

        Assert.Equal("Driveway: 1x person", digest);
    }

    [Fact]
    public void ComputeDelayUntilNext_TimeLaterToday_DelaysUntilThatTimeToday()
    {
        var now = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);

        var delay = DailyDigestService.ComputeDelayUntilNext("20:00", now);

        Assert.Equal(TimeSpan.FromHours(10), delay);
    }

    [Fact]
    public void ComputeDelayUntilNext_TimeAlreadyPassedToday_DelaysUntilTomorrow()
    {
        var now = new DateTimeOffset(2026, 8, 22, 21, 0, 0, TimeSpan.Zero);

        var delay = DailyDigestService.ComputeDelayUntilNext("20:00", now);

        Assert.Equal(TimeSpan.FromHours(23), delay);
    }

    [Fact]
    public void ComputeDelayUntilNext_MalformedTimeOfDay_FallsBackTo2000()
    {
        var now = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);

        var delay = DailyDigestService.ComputeDelayUntilNext("not-a-time", now);

        Assert.Equal(TimeSpan.FromHours(10), delay);
    }

    [Fact]
    public void ComputeSince_NoWatermark_StartsFromLookbackBound()
    {
        var now = new DateTime(2026, 8, 22, 20, 0, 0, DateTimeKind.Utc);
        var lookback = TimeSpan.FromHours(48);

        var since = DailyDigestService.ComputeSince(watermark: null, now, lookback);

        Assert.Equal(now - lookback, since);
    }

    [Fact]
    public void ComputeSince_RecentWatermark_ResumesFromWatermark()
    {
        var now = new DateTime(2026, 8, 22, 20, 0, 0, DateTimeKind.Utc);
        var watermark = now.AddHours(-24);

        var since = DailyDigestService.ComputeSince(watermark, now, TimeSpan.FromHours(48));

        Assert.Equal(watermark, since);
    }

    [Fact]
    public void ComputeSince_StaleWatermark_ClampsToLookbackBound()
    {
        var now = new DateTime(2026, 8, 22, 20, 0, 0, DateTimeKind.Utc);
        var staleWatermark = now - TimeSpan.FromDays(30);
        var lookback = TimeSpan.FromHours(48);

        var since = DailyDigestService.ComputeSince(staleWatermark, now, lookback);

        Assert.Equal(now - lookback, since);
    }
}
