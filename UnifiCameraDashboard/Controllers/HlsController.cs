using Microsoft.AspNetCore.Mvc;
using UnifiCameraDashboard.Services;
using System.Text.Json;

namespace UnifiCameraDashboard.Controllers;

[ApiController]
[Route("api/hls")]
public class HlsController : ControllerBase
{
    private readonly IFfmpegService _ffmpegService;
    private readonly ISettingsService _settingsService;
    private readonly IUnifiCameraService _cameraService;
    private readonly ILogger<HlsController> _logger;

    public HlsController(
   IFfmpegService ffmpegService,
  ISettingsService settingsService,
        IUnifiCameraService cameraService,
     ILogger<HlsController> logger)
    {
        _ffmpegService = ffmpegService;
        _settingsService = settingsService;
        _cameraService = cameraService;
        _logger = logger;
    }

    /// <summary>
    /// Starts HLS stream for a camera
    /// </summary>
    [HttpGet("start/{cameraId}")]
    public async Task<IActionResult> StartStream(string cameraId, [FromQuery] int channel = 0)
    {
        try
        {
            // Hole Kamera-Info
            var camera = await _cameraService.GetCameraByIdAsync(cameraId);
            if (camera == null)
            {
                return NotFound($"Camera {cameraId} not found");
            }

            // Use stored RTSP URL from discovery (most reliable)
            var rtspUrl = camera.RtspUrl;

            if (string.IsNullOrEmpty(rtspUrl))
            {
                // Fallback: build RTSP URL manually (if not in DB)
                var username = await _settingsService.GetUsernameAsync();
                var password = await _settingsService.GetPasswordAsync();
                var baseUrl = await _settingsService.GetUnifiProtectUrlAsync();
                var host = baseUrl?.Replace("https://", "").Replace("http://", "");

                // Try standard RTSP port 554 (often works better than 7447)
                rtspUrl = $"rtsp://{username}:{password}@{host}:554/{cameraId}";
            }

            _logger.LogInformation("Starting HLS stream for camera {Name} ({Id}), Channel: {Channel}",
                camera.Name, cameraId, channel);
            _logger.LogInformation("RTSP URL (from DB): {RtspUrl}", rtspUrl.Replace(":", "***").Substring(0, Math.Min(50, rtspUrl.Length)) + "...");

            var playlistPath = await _ffmpegService.StartHlsStreamAsync(cameraId, rtspUrl);

            if (playlistPath == null)
            {
                return StatusCode(500, "Stream could not be started");
            }

            return Ok(new
            {
                success = true,
                cameraId,
                cameraName = camera.Name,
                playlistUrl = playlistPath,
                message = "HLS stream started"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting HLS stream for camera {CameraId}", cameraId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Stops HLS stream for a camera
    /// </summary>
    [HttpPost("stop/{cameraId}")]
    public IActionResult StopStream(string cameraId)
    {
        try
        {
            _logger.LogInformation("Stopping HLS stream for camera {CameraId}", cameraId);
            _ffmpegService.StopHlsStream(cameraId);

            return Ok(new
            {
                success = true,
                cameraId,
                message = "HLS stream stopped"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping HLS stream for camera {CameraId}", cameraId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Shows status of all active streams
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        try
        {
            var activeStreams = _ffmpegService.GetActiveStreams();

            return Ok(new
            {
                success = true,
                activeStreamCount = activeStreams.Count,
                streams = activeStreams.Select(kvp => new
                {
                    cameraId = kvp.Key,
                    rtspUrl = kvp.Value.RtspUrl,
                    playlistPath = kvp.Value.PlaylistPath,
                    startTime = kvp.Value.StartTime,
                    uptime = DateTime.Now - kvp.Value.StartTime,
                    isRunning = kvp.Value.IsRunning
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving stream status");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Starts all camera streams
    /// </summary>
    [HttpPost("start-all")]
    public async Task<IActionResult> StartAllStreams([FromQuery] int channel = 0)
    {
        try
        {
            var cameras = await _cameraService.GetCamerasAsync();
            var results = new List<object>();

            // Hole Credentials einmal
            var username = await _settingsService.GetUsernameAsync();
            var password = await _settingsService.GetPasswordAsync();
            var baseUrl = await _settingsService.GetUnifiProtectUrlAsync();
            var host = baseUrl?.Replace("https://", "").Replace("http://", "");

            foreach (var camera in cameras.Where(c => c.IsOnline))
            {
                try
                {
                    // RTSP URL (ohne TLS) wie Home Assistant
                    var rtspUrl = $"rtsp://{username}:{password}@{host}:7447/{camera.Id}?channel={channel}";
                    var playlistPath = await _ffmpegService.StartHlsStreamAsync(camera.Id, rtspUrl);

                    results.Add(new
                    {
                        cameraId = camera.Id,
                        cameraName = camera.Name,
                        success = playlistPath != null,
                        playlistUrl = playlistPath
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error starting stream for camera {Id}", camera.Id);
                    results.Add(new
                    {
                        cameraId = camera.Id,
                        cameraName = camera.Name,
                        success = false,
                        error = ex.Message
                    });
                }
            }

            return Ok(new
            {
                success = true,
                totalCameras = cameras.Count,
                startedStreams = results.Count(r => (bool)((dynamic)r).success),
                results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting all streams");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Stops all active streams
    /// </summary>
    [HttpPost("stop-all")]
    public IActionResult StopAllStreams()
    {
        try
        {
            var activeStreams = _ffmpegService.GetActiveStreams();
            var cameraIds = activeStreams.Keys.ToList();

            foreach (var cameraId in cameraIds)
            {
                _ffmpegService.StopHlsStream(cameraId);
            }

            return Ok(new
            {
                success = true,
                stoppedStreams = cameraIds.Count,
                cameraIds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping all streams");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Diagnostics: Check if HLS files exist for a camera
    /// </summary>
    [HttpGet("debug/{cameraId}")]
    public IActionResult DebugStream(string cameraId)
    {
        try
        {
            var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var hlsDir = Path.Combine(wwwroot, "hls", cameraId);
            var playlistFile = Path.Combine(hlsDir, "stream.m3u8");

            object? files = null;
            if (Directory.Exists(hlsDir))
            {
                files = Directory.GetFiles(hlsDir).Select(f => new
                {
                    name = Path.GetFileName(f),
                    size = new FileInfo(f).Length,
                    created = new FileInfo(f).CreationTime,
                    modified = new FileInfo(f).LastWriteTime
                }).ToList();
            }

            var debugInfo = new
            {
                cameraId,
                wwwrootPath = wwwroot,
                hlsDirectory = hlsDir,
                directoryExists = Directory.Exists(hlsDir),
                playlistFile,
                playlistExists = System.IO.File.Exists(playlistFile),
                files,
                fileCount = (files as dynamic)?.Count ?? 0,
                streamActive = _ffmpegService.IsStreamActive(cameraId)
            };

            return Ok(debugInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during debug for camera {CameraId}", cameraId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Serve HLS playlist directly (bypasses static files middleware)
    /// </summary>
    [HttpGet("/hls/{cameraId}/stream.m3u8")]
    public IActionResult ServePlaylist(string cameraId)
    {
        try
        {
            var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var playlistFile = Path.Combine(wwwroot, "hls", cameraId, "stream.m3u8");

            _logger.LogInformation("HLS Playlist Request: {CameraId} -> {File}", cameraId, playlistFile);

            if (!System.IO.File.Exists(playlistFile))
            {
                _logger.LogWarning("Playlist not found: {File}", playlistFile);
                return NotFound(new { error = "Playlist not found", file = playlistFile });
            }

            // Read playlist and log content
            var content = System.IO.File.ReadAllText(playlistFile);
            _logger.LogDebug("Playlist content:\n{Content}", content);

            return PhysicalFile(playlistFile, "application/vnd.apple.mpegurl", enableRangeProcessing: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving playlist for camera {CameraId}", cameraId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Serve HLS segments directly (bypasses static files middleware)
    /// </summary>
    [HttpGet("/hls/{cameraId}/{filename}")]
    public IActionResult ServeSegment(string cameraId, string filename)
    {
        try
        {
            // Only allow .ts files
            if (!filename.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Invalid file type");
            }

            var wwwroot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var segmentFile = Path.Combine(wwwroot, "hls", cameraId, filename);

            _logger.LogDebug("HLS Segment Request: {CameraId}/{Filename} -> {File}", cameraId, filename, segmentFile);

            if (!System.IO.File.Exists(segmentFile))
            {
                _logger.LogWarning("Segment not found: {File}", segmentFile);
                return NotFound(new { error = "Segment not found", file = segmentFile });
            }

            return PhysicalFile(segmentFile, "video/MP2T", enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving segment {Filename} for camera {CameraId}", filename, cameraId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
