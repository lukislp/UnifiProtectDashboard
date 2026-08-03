using Microsoft.AspNetCore.Mvc;
using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Controllers;

[ApiController]
[Route("api/discovery")]
public class DiscoveryController : ControllerBase
{
    private readonly IUnifiCameraService _cameraService;
    private readonly ILogger<DiscoveryController> _logger;

    public DiscoveryController(
        IUnifiCameraService cameraService,
      ILogger<DiscoveryController> logger)
    {
        _cameraService = cameraService;
        _logger = logger;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartDiscovery()
    {
        try
        {
            _logger.LogInformation("API: Camera discovery started");
            var cameras = await _cameraService.DiscoverCamerasAsync();

            return Ok(new
            {
                success = true,
                message = $"{cameras.Count} cameras found",
                count = cameras.Count,
                cameras = cameras.Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.MacAddress,
                    c.Model,
                    c.IsOnline,
                    c.SnapshotUrl,
                    c.RtspUrl
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during discovery");
            return StatusCode(500, new
            {
                success = false,
                message = "Error discovering cameras",
                error = ex.Message
            });
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            status = "ready",
            message = "Discovery API is ready",
            timestamp = DateTime.UtcNow
        });
    }
}
