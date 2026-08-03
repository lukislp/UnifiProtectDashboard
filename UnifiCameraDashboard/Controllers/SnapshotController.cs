using Microsoft.AspNetCore.Mvc;
using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Controllers;

[ApiController]
[Route("api/snapshot")]
public class SnapshotController : ControllerBase
{
    private readonly IUnifiProtectService _protectService;
    private readonly ILogger<SnapshotController> _logger;

    public SnapshotController(IUnifiProtectService protectService, ILogger<SnapshotController> logger)
    {
        _protectService = protectService;
        _logger = logger;
    }

    [HttpGet("{cameraId}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetSnapshot(string cameraId, [FromQuery] int? w = null, [FromQuery] int? h = null)
    {
        var result = await _protectService.GetSnapshotAsync(cameraId, w, h);

        if (result == null)
        {
            _logger.LogWarning("Snapshot for camera {CameraId} could not be loaded", cameraId);
            return StatusCode(503, "Snapshot not available");
        }

        Response.Headers.Append("Cache-Control", "no-cache, must-revalidate");
        Response.Headers.Append("Pragma", "no-cache");
        Response.Headers.Append("Expires", "0");

        return File(result.Value.Data, result.Value.ContentType);
    }
}
