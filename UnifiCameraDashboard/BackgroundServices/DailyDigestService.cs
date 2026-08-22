using System.Globalization;
using NotifyHub;
using UnifiCameraDashboard.Models;
using UnifiCameraDashboard.Services;
using UnifiCameraDashboard.Services.Notifications;

namespace UnifiCameraDashboard.BackgroundServices;

/// <summary>
/// Sends a rule-based daily summary of YOLO detection counts per camera via Web Push (see
/// Controllers/PushController.cs, Services/Notifications/) - S3 of the original roadmap.
/// Computes the delay until the next configured time-of-day itself (no existing "run once daily"
/// pattern in this codebase - the other background services are all fixed-interval polling
/// loops) and recomputes it every cycle, so a settings change takes effect on the next run
/// without a restart.
/// </summary>
public class DailyDigestService : BackgroundService
{
    private const string DigestWatermarkKey = "DailyDigest:CompletedThroughUtc";

    // Clamps the resume window the same way EventIngestionService's backfill watermark does -
    // a pod down for days (or a first-ever run) shouldn't try to summarize weeks of history.
    private static readonly TimeSpan MaxLookback = TimeSpan.FromHours(48);

    private readonly IServiceProvider _serviceProvider;
    private readonly NotificationSender _notificationSender;
    private readonly IInstanceLock _instanceLock;
    private readonly ILogger<DailyDigestService> _logger;

    public DailyDigestService(
        IServiceProvider serviceProvider,
        NotificationSender notificationSender,
        IInstanceLock instanceLock,
        ILogger<DailyDigestService> logger)
    {
        _serviceProvider = serviceProvider;
        _notificationSender = notificationSender;
        _instanceLock = instanceLock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Same reasoning as EventIngestionService/EventClassificationService: during a rolling
        // update both pods are briefly up together, but only one should broadcast the digest.
        await _instanceLock.WhenAcquiredAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            using (var scope = _serviceProvider.CreateScope())
            {
                var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
                var timeOfDay = await settingsService.GetDailyDigestTimeOfDayAsync();
                delay = ComputeDelayUntilNext(timeOfDay, DateTimeOffset.Now);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await SendDigestAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send the daily digest");
            }
        }
    }

    private async Task SendDigestAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();

        if (!await settingsService.GetDailyDigestEnabledAsync())
        {
            return;
        }

        var subscriptionRepository = scope.ServiceProvider.GetRequiredService<IPushSubscriptionRepository>();
        var subscriptions = await subscriptionRepository.GetAllAsync();
        if (subscriptions.Count == 0)
        {
            _logger.LogInformation("Daily digest is enabled but no push subscriptions are registered - skipping");
            return;
        }

        var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var cameraRepository = scope.ServiceProvider.GetRequiredService<ICameraRepository>();

        var watermark = await GetWatermarkAsync(settingsService);
        var since = ComputeSince(watermark, DateTime.UtcNow, MaxLookback);
        var requestEnd = DateTime.UtcNow;

        var counts = await eventRepository.GetLabelCountsSinceAsync(since);
        var cameras = await cameraRepository.GetAllCamerasAsync();

        var body = FormatDigest(cameras, counts);

        var message = new NotificationMessage
        {
            Title = "Daily Digest",
            Body = body,
        };

        var results = await _notificationSender.SendAsync(
            message,
            subscriptions.Select(s => Subscription.WebPush(s.Endpoint, s.P256dh, s.Auth, id: s.Id.ToString())),
            ct: cancellationToken);

        foreach (var (subscription, result) in subscriptions.Zip(results))
        {
            if (result.Outcome == SendOutcome.Expired)
            {
                await subscriptionRepository.RemoveAsync(subscription.Endpoint);
            }
        }

        await settingsService.SetSettingAsync(DigestWatermarkKey, requestEnd.ToString("O"));
        _logger.LogInformation("Daily digest sent to {Count} subscription(s): {Body}", subscriptions.Count, body);
    }

    /// <summary>
    /// "{Name}: {count}x {label}, ..." per camera (highest count first), or "{Name}: quiet" for
    /// zero labeled detections, all cameras joined with "; ". Pure/testable, no I/O - cameras
    /// already comes from CameraRepository.GetAllCamerasAsync(), which is Enabled-filtered, so a
    /// removed camera drops out of the digest for free.
    /// </summary>
    public static string FormatDigest(
        IReadOnlyList<UnifiCamera> cameras,
        IReadOnlyList<(string CameraUnifiId, string Label, int Count)> counts)
    {
        var byCamera = counts.ToLookup(c => c.CameraUnifiId);

        var parts = cameras.Select(camera =>
        {
            var cameraCounts = byCamera[camera.Id].ToList();
            if (cameraCounts.Count == 0)
            {
                return $"{camera.Name}: quiet";
            }

            var labelParts = cameraCounts
                .OrderByDescending(c => c.Count)
                .Select(c => $"{c.Count}x {c.Label}");
            return $"{camera.Name}: {string.Join(", ", labelParts)}";
        });

        return string.Join("; ", parts);
    }

    /// <summary>Delay until the next occurrence of "HH:mm" (falls back to 20:00 if malformed).</summary>
    public static TimeSpan ComputeDelayUntilNext(string timeOfDay, DateTimeOffset now)
    {
        if (!TimeSpan.TryParseExact(timeOfDay, @"hh\:mm", CultureInfo.InvariantCulture, out var timeOfDayOffset))
        {
            timeOfDayOffset = new TimeSpan(20, 0, 0);
        }

        var next = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset).Add(timeOfDayOffset);
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }

    /// <summary>Clamps the resume point to the configured lookback window - same reasoning as EventIngestionService.ComputeBackfillStart.</summary>
    public static DateTime ComputeSince(DateTime? watermark, DateTime now, TimeSpan maxLookback)
    {
        var earliestAllowed = now - maxLookback;
        var since = watermark ?? earliestAllowed;
        return since < earliestAllowed ? earliestAllowed : since;
    }

    private static async Task<DateTime?> GetWatermarkAsync(ISettingsService settingsService)
    {
        var raw = await settingsService.GetSettingAsync(DigestWatermarkKey);
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }
}
