using System.Text.Json;
using UnifiCameraDashboard.Services;
using UnifiCameraDashboard.Services.Protect;

namespace UnifiCameraDashboard.BackgroundServices;

/// <summary>Where local instance data lives - registered once in Program.cs from the resolved DATA_DIR.</summary>
public sealed record DataDirectoryOptions(string Path);

/// <summary>
/// Consumes the Protect realtime updates websocket (via <see cref="IProtectWebSocketClient"/>)
/// and persists "event" model updates. Also periodically backfills via the REST events endpoint,
/// which covers both startup and any gap from a websocket reconnect - simpler and more robust
/// than hooking backfill to the client's own reconnect cycle, since that could reconnect
/// frequently under a flaky network and thrash the backfill call.
/// </summary>
public class EventIngestionService : BackgroundService
{
    private static readonly TimeSpan BackfillInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MaxBackfillLookback = TimeSpan.FromHours(24);
    private static readonly TimeSpan MaxOpenEventDuration = TimeSpan.FromHours(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly DataDirectoryOptions _dataDirectory;
    private readonly IClassificationQueue _classificationQueue;
    private readonly ILogger<EventIngestionService> _logger;

    public EventIngestionService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        DataDirectoryOptions dataDirectory,
        IClassificationQueue classificationQueue,
        ILogger<EventIngestionService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _dataDirectory = dataDirectory;
        _classificationQueue = classificationQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("EventIngestion:Enabled", true))
        {
            _logger.LogInformation("Event ingestion disabled via configuration (EventIngestion:Enabled=false)");
            return;
        }

        // One scope for the websocket client's whole lifetime - it only depends on
        // IUnifiProtectService, which doesn't accumulate EF change-tracker state between calls.
        // Per-update DB work below opens its own short-lived scope instead (see HandleUpdateAsync)
        // so a single DbContext's change tracker never grows across the connection's lifetime.
        using var clientScope = _serviceProvider.CreateScope();
        var webSocketClient = clientScope.ServiceProvider.GetRequiredService<IProtectWebSocketClient>();

        var backfillLoop = PeriodicBackfillLoopAsync(stoppingToken);
        var receiveLoop = webSocketClient.RunAsync(HandleUpdateAsync, stoppingToken);

        await Task.WhenAll(backfillLoop, receiveLoop);
    }

    private async Task HandleUpdateAsync(ProtectUpdate update)
    {
        if (!string.Equals(update.Action.ModelKey, "event", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (update.Action.Action is not ("add" or "update"))
        {
            return;
        }
        if (update.JsonData is not { } data)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();

        (int Id, bool IsNew) result;
        try
        {
            result = await eventRepository.UpsertFromWebSocketAsync(update.Action.Id, data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upsert event {EventId} from a websocket update", update.Action.Id);
            return;
        }

        if (result.IsNew)
        {
            await FetchThumbnailAsync(scope.ServiceProvider, update.Action.Id, data, eventRepository);
        }
    }

    private async Task FetchThumbnailAsync(IServiceProvider scopedProvider, string unifiEventId, JsonElement data, IEventRepository eventRepository)
    {
        if (!data.TryGetProperty("camera", out var cameraProp) || cameraProp.ValueKind != JsonValueKind.String)
        {
            return; // not camera-scoped (e.g. an NVR "access" event) - nothing to snapshot
        }

        var cameraId = cameraProp.GetString();
        if (string.IsNullOrEmpty(cameraId))
        {
            return;
        }

        try
        {
            var protectService = scopedProvider.GetRequiredService<IUnifiProtectService>();
            var snapshot = await protectService.GetSnapshotAsync(cameraId, width: 640, height: 360);
            if (snapshot == null)
            {
                return;
            }

            var thumbnailDir = Path.Combine(_dataDirectory.Path, "thumbnails");
            Directory.CreateDirectory(thumbnailDir);
            var path = Path.Combine(thumbnailDir, $"{unifiEventId}.jpg");
            await File.WriteAllBytesAsync(path, snapshot.Value.Data);

            await eventRepository.SetThumbnailPathAsync(unifiEventId, path);
            _classificationQueue.TryEnqueue(new ClassificationRequest(unifiEventId, snapshot.Value.Data));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch thumbnail for event {EventId}", unifiEventId);
        }
    }

    private async Task PeriodicBackfillLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await BackfillMissedEventsAsync(stoppingToken);
            await CloseStaleOpenEventsAsync();

            try
            {
                await Task.Delay(BackfillInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task BackfillMissedEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var protectService = scope.ServiceProvider.GetRequiredService<IUnifiProtectService>();
        var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();

        var earliestAllowed = DateTimeOffset.UtcNow - MaxBackfillLookback;
        var latestKnown = await eventRepository.GetLatestEventStartAsync();
        var start = latestKnown.HasValue ? new DateTimeOffset(latestKnown.Value, TimeSpan.Zero) : earliestAllowed;
        if (start < earliestAllowed)
        {
            start = earliestAllowed;
        }

        var events = await protectService.GetEventsAsync(start, DateTimeOffset.UtcNow);
        _logger.LogInformation("Backfilling {Count} event(s) since {Start}", events.Count, start);

        foreach (var evt in events)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var (_, needsThumbnail) = await eventRepository.UpsertFromRestAsync(evt);
                if (needsThumbnail)
                {
                    // Deliberately sequential (no concurrency added here) - this can catch up
                    // hundreds of historical events on first deploy, and both Protect (many
                    // requests in a burst) and the classification queue (bounded, single
                    // consumer) are better served by pacing this the same way the rest of this
                    // loop already does, one event at a time.
                    await BackfillThumbnailAsync(protectService, eventRepository, evt.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to backfill event {EventId}", evt.Id);
            }
        }
    }

    private async Task BackfillThumbnailAsync(IUnifiProtectService protectService, IEventRepository eventRepository, string unifiEventId)
    {
        try
        {
            var bytes = await protectService.GetEventThumbnailAsync(unifiEventId, width: 640, height: 360);
            if (bytes == null)
            {
                return;
            }

            var thumbnailDir = Path.Combine(_dataDirectory.Path, "thumbnails");
            Directory.CreateDirectory(thumbnailDir);
            var path = Path.Combine(thumbnailDir, $"{unifiEventId}.jpg");
            await File.WriteAllBytesAsync(path, bytes);

            await eventRepository.SetThumbnailPathAsync(unifiEventId, path);
            _classificationQueue.TryEnqueue(new ClassificationRequest(unifiEventId, bytes));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to backfill thumbnail for event {EventId}", unifiEventId);
        }
    }

    private async Task CloseStaleOpenEventsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
        var closed = await eventRepository.CloseStaleOpenEventsAsync(MaxOpenEventDuration);
        if (closed > 0)
        {
            _logger.LogInformation("Closed {Count} stale open event(s)", closed);
        }
    }
}
