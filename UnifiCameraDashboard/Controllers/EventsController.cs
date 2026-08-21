using Microsoft.AspNetCore.Mvc;
using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private const int MaxTake = 200;

    private readonly IEventRepository _eventRepository;
    private readonly ICameraRepository _cameraRepository;
    private readonly ILogger<EventsController> _logger;

    public EventsController(IEventRepository eventRepository, ICameraRepository cameraRepository, ILogger<EventsController> logger)
    {
        _eventRepository = eventRepository;
        _cameraRepository = cameraRepository;
        _logger = logger;
    }

    /// <summary>
    /// Chronological event list (newest first), optionally filtered by camera and/or type.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetEvents([FromQuery] int skip = 0, [FromQuery] int take = 50, [FromQuery] string? cameraId = null, [FromQuery] string? type = null, [FromQuery] string? label = null)
    {
        try
        {
            skip = Math.Max(skip, 0);
            take = Math.Clamp(take, 1, MaxTake);

            var events = await _eventRepository.GetRecentEventsAsync(skip, take, cameraId, type, label);
            var cameraNames = (await _cameraRepository.GetAllCamerasAsync()).ToDictionary(c => c.Id, c => c.Name);

            var dto = events.Select(e => new
            {
                id = e.UnifiEventId,
                cameraId = e.CameraUnifiId,
                cameraName = e.CameraUnifiId != null && cameraNames.TryGetValue(e.CameraUnifiId, out var name) ? name : null,
                type = e.Type,
                smartDetectTypes = string.IsNullOrEmpty(e.SmartDetectTypes) ? [] : e.SmartDetectTypes.Split(','),
                yoloLabels = string.IsNullOrEmpty(e.YoloLabels) ? [] : e.YoloLabels.Split(','),
                score = e.Score,
                start = e.Start,
                end = e.End,
                thumbnailUrl = e.ThumbnailPath != null ? $"/api/events/{e.UnifiEventId}/thumbnail" : null
            });

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving events");
            return StatusCode(500, new { error = "Error loading events", details = ex.Message });
        }
    }

    /// <summary>
    /// Proxies an event's saved thumbnail - keeps the on-disk path out of client-facing responses.
    /// </summary>
    [HttpGet("{id}/thumbnail")]
    public async Task<IActionResult> GetThumbnail(string id)
    {
        try
        {
            var evt = await _eventRepository.GetByUnifiEventIdAsync(id);
            if (evt?.ThumbnailPath == null || !System.IO.File.Exists(evt.ThumbnailPath))
            {
                return NotFound();
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(evt.ThumbnailPath);
            return File(bytes, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving thumbnail for event {EventId}", id);
            return StatusCode(500, new { error = "Error loading thumbnail" });
        }
    }
}
