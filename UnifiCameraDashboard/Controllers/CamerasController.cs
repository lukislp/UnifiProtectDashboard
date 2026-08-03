using Microsoft.AspNetCore.Mvc;
using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CamerasController : ControllerBase
{
    private readonly IUnifiCameraService _cameraService;
    private readonly ILogger<CamerasController> _logger;

    public CamerasController(IUnifiCameraService cameraService, ILogger<CamerasController> logger)
    {
        _cameraService = cameraService;
        _logger = logger;
    }

    /// <summary>
    /// Get all cameras (for Static Dashboard)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCameras()
    {
        try
        {
            var cameras = await _cameraService.GetCamerasAsync();

            // Simplified DTO for Static Dashboard
            var camerasDto = cameras.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                model = c.Model,
                isOnline = c.IsOnline,
                width = c.Width,
                height = c.Height,
                rtspUrl = c.RtspUrl,
                snapshotUrl = $"/api/snapshot/{c.Id}"
            }).ToList();

            _logger.LogInformation("API: {Count} cameras retrieved for Static Dashboard", camerasDto.Count);

            return Ok(camerasDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cameras");
            return StatusCode(500, new { error = "Error loading cameras", details = ex.Message });
        }
    }

    /// <summary>
    /// Get single camera
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetCamera(string id)
    {
        try
        {
            var cameras = await _cameraService.GetCamerasAsync();
            var camera = cameras.FirstOrDefault(c => c.Id == id);

            if (camera == null)
            {
                return NotFound(new { error = "Camera not found" });
            }

            var cameraDto = new
            {
                id = camera.Id,
                name = camera.Name,
                model = camera.Model,
                isOnline = camera.IsOnline,
                width = camera.Width,
                height = camera.Height,
                rtspUrl = camera.RtspUrl,
                snapshotUrl = $"/api/snapshot/{camera.Id}"
            };

            return Ok(cameraDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving camera {CameraId}", id);
            return StatusCode(500, new { error = "Error loading camera" });
        }
    }

    /// <summary>
    /// Check connection status
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            var isConnected = await _cameraService.TestConnectionAsync();
            var cameras = await _cameraService.GetCamerasAsync();

            return Ok(new
            {
                connected = isConnected,
                cameraCount = cameras.Count,
                onlineCameras = cameras.Count(c => c.IsOnline),
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during status check");
            return Ok(new
            {
                connected = false,
                error = ex.Message
            });
        }
    }
}
