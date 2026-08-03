using Microsoft.AspNetCore.Mvc;

namespace UnifiCameraDashboard.Controllers;

[ApiController]
[Route("api")]
public class PingController : ControllerBase
{
    private readonly ILogger<PingController> _logger;

    public PingController(ILogger<PingController> logger)
    {
        _logger = logger;
    }

    [HttpHead("ping")]
    [HttpGet("ping")]
    public IActionResult Ping()
    {
        _logger.LogDebug("Ping request received - Keep-Alive");
        return Ok(new { status = "alive", timestamp = DateTime.UtcNow });
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            uptime = Environment.TickCount64 / 1000 // seconds
        });
    }
}
