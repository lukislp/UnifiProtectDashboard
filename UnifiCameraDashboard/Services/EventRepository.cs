using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UnifiCameraDashboard.Data;
using UnifiCameraDashboard.Models;

namespace UnifiCameraDashboard.Services;

public interface IEventRepository
{
    /// <summary>
    /// Applies a decoded websocket update to the matching event, creating it if this is the
    /// first time it's been seen. <paramref name="data"/> is a partial patch on "update"
    /// actions (only changed fields are present) and a full object on "add" - both are handled
    /// by only touching fields that are actually present in the JSON.
    /// </summary>
    Task<(int Id, bool IsNew)> UpsertFromWebSocketAsync(string unifiEventId, JsonElement data);

    /// <summary>
    /// Applies a full event object from the REST backfill endpoint - unlike the websocket path,
    /// every field is always present here, so it's a straight overwrite. NeedsThumbnail is true
    /// when this event is camera-scoped and doesn't have a thumbnail yet, saving the caller a
    /// separate lookup to decide whether to backfill one.
    /// </summary>
    Task<(int Id, bool NeedsThumbnail)> UpsertFromRestAsync(ProtectEventPayload payload);

    Task SetThumbnailPathAsync(string unifiEventId, string thumbnailPath);

    /// <summary>Records YOLO classification results - an empty list is a real, meaningful outcome (nothing detected above threshold), not skipped.</summary>
    Task SetYoloLabelsAsync(string unifiEventId, IReadOnlyList<string> labels);

    Task<StoredEvent?> GetByUnifiEventIdAsync(string unifiEventId);
    Task<List<StoredEvent>> GetRecentEventsAsync(int skip, int take, string? cameraId = null, string? type = null, string? yoloLabel = null);

    /// <summary>
    /// Marks events that never received an "end" update (e.g. after a crash mid-event) as
    /// closed, using their own start time as a duration-unknown approximation, so they don't
    /// stay "in progress" forever in the UI. Returns how many rows were closed.
    /// </summary>
    Task<int> CloseStaleOpenEventsAsync(TimeSpan maxOpenDuration);
}

public class EventRepository : IEventRepository
{
    private readonly DashboardDbContext _context;
    private readonly ILogger<EventRepository> _logger;

    public EventRepository(DashboardDbContext context, ILogger<EventRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(int Id, bool IsNew)> UpsertFromWebSocketAsync(string unifiEventId, JsonElement data)
    {
        try
        {
            var existing = await _context.Events.FirstOrDefaultAsync(e => e.UnifiEventId == unifiEventId);
            var isNew = existing == null;
            var entity = existing ?? new StoredEvent { UnifiEventId = unifiEventId, CreatedAt = DateTime.UtcNow };

            ApplyPresentFields(entity, data);
            entity.UpdatedAt = DateTime.UtcNow;

            if (isNew)
            {
                _context.Events.Add(entity);
            }

            await _context.SaveChangesAsync();
            return (entity.Id, isNew);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting event {UnifiEventId} from websocket", unifiEventId);
            throw;
        }
    }

    public async Task<(int Id, bool NeedsThumbnail)> UpsertFromRestAsync(ProtectEventPayload payload)
    {
        try
        {
            var existing = await _context.Events.FirstOrDefaultAsync(e => e.UnifiEventId == payload.Id);
            var entity = existing ?? new StoredEvent { UnifiEventId = payload.Id, CreatedAt = DateTime.UtcNow };

            entity.Type = payload.Type;
            entity.CameraUnifiId = payload.Camera;
            entity.Score = payload.Score;
            entity.Start = DateTimeOffset.FromUnixTimeMilliseconds(payload.Start).UtcDateTime;
            entity.End = payload.End.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(payload.End.Value).UtcDateTime
                : null;
            entity.SmartDetectTypes = string.Join(",", payload.SmartDetectTypes);
            entity.UpdatedAt = DateTime.UtcNow;

            if (existing == null)
            {
                _context.Events.Add(entity);
            }

            await _context.SaveChangesAsync();

            var needsThumbnail = entity.CameraUnifiId != null && entity.ThumbnailPath == null;
            return (entity.Id, needsThumbnail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting event {UnifiEventId} from REST backfill", payload.Id);
            throw;
        }
    }

    public async Task SetThumbnailPathAsync(string unifiEventId, string thumbnailPath)
    {
        try
        {
            var entity = await _context.Events.FirstOrDefaultAsync(e => e.UnifiEventId == unifiEventId);
            if (entity == null)
            {
                return;
            }

            entity.ThumbnailPath = thumbnailPath;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving thumbnail path for event {UnifiEventId}", unifiEventId);
        }
    }

    public async Task SetYoloLabelsAsync(string unifiEventId, IReadOnlyList<string> labels)
    {
        try
        {
            var entity = await _context.Events.FirstOrDefaultAsync(e => e.UnifiEventId == unifiEventId);
            if (entity == null)
            {
                return;
            }

            entity.YoloLabels = string.Join(",", labels);
            entity.YoloClassifiedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving YOLO labels for event {UnifiEventId}", unifiEventId);
        }
    }

    public async Task<StoredEvent?> GetByUnifiEventIdAsync(string unifiEventId)
    {
        try
        {
            return await _context.Events.FirstOrDefaultAsync(e => e.UnifiEventId == unifiEventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving event {UnifiEventId}", unifiEventId);
            return null;
        }
    }

    public async Task<List<StoredEvent>> GetRecentEventsAsync(int skip, int take, string? cameraId = null, string? type = null, string? yoloLabel = null)
    {
        try
        {
            var query = _context.Events.AsQueryable();

            if (!string.IsNullOrEmpty(cameraId))
            {
                query = query.Where(e => e.CameraUnifiId == cameraId);
            }
            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(e => e.Type == type);
            }
            if (!string.IsNullOrEmpty(yoloLabel))
            {
                // CSV column, same simple-substring-match convention as SmartDetectTypes elsewhere.
                query = query.Where(e => e.YoloLabels.Contains(yoloLabel));
            }

            return await query
                .OrderByDescending(e => e.Start)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent events");
            return new List<StoredEvent>();
        }
    }

    public async Task<int> CloseStaleOpenEventsAsync(TimeSpan maxOpenDuration)
    {
        try
        {
            var cutoff = DateTime.UtcNow - maxOpenDuration;
            var staleEvents = await _context.Events
                .Where(e => e.End == null && e.Start < cutoff)
                .ToListAsync();

            foreach (var evt in staleEvents)
            {
                evt.End = evt.Start;
                evt.UpdatedAt = DateTime.UtcNow;
            }

            if (staleEvents.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            return staleEvents.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing stale open events");
            return 0;
        }
    }

    private static void ApplyPresentFields(StoredEvent entity, JsonElement data)
    {
        if (data.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
        {
            entity.Type = type.GetString() ?? entity.Type;
        }

        if (data.TryGetProperty("camera", out var camera) && camera.ValueKind == JsonValueKind.String)
        {
            entity.CameraUnifiId = camera.GetString();
        }

        if (data.TryGetProperty("score", out var score) && score.ValueKind == JsonValueKind.Number)
        {
            entity.Score = score.GetInt32();
        }

        if (data.TryGetProperty("start", out var start) && start.ValueKind == JsonValueKind.Number)
        {
            entity.Start = DateTimeOffset.FromUnixTimeMilliseconds(start.GetInt64()).UtcDateTime;
        }

        if (data.TryGetProperty("end", out var end) && end.ValueKind == JsonValueKind.Number)
        {
            entity.End = DateTimeOffset.FromUnixTimeMilliseconds(end.GetInt64()).UtcDateTime;
        }

        if (data.TryGetProperty("smartDetectTypes", out var types) && types.ValueKind == JsonValueKind.Array)
        {
            entity.SmartDetectTypes = string.Join(",", types.EnumerateArray()
                .Where(t => t.ValueKind == JsonValueKind.String)
                .Select(t => t.GetString()));
        }
    }
}
