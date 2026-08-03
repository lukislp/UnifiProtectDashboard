using Microsoft.AspNetCore.Mvc;
using UnifiCameraDashboard.Services;
using System.Collections.Concurrent;

namespace UnifiCameraDashboard.Controllers;

[ApiController]
[Route("api/stream")]
public class StreamController : ControllerBase
{
    private readonly IUnifiProtectService _protectService;
    private readonly ILogger<StreamController> _logger;

    private static readonly ConcurrentDictionary<string, (byte[] Frame, DateTime Timestamp)> _frameCache = new();
    private static readonly TimeSpan _cacheLifetime = TimeSpan.FromMilliseconds(100);

    public StreamController(
        IUnifiProtectService protectService,
        ILogger<StreamController> logger)
    {
        _protectService = protectService;
        _logger = logger;
    }

    /// <summary>
    /// MJPEG stream composed from repeated snapshot fetches.
    /// </summary>
    [HttpGet("mjpeg/{cameraId}")]
    public async Task GetMjpegStream(
        string cameraId,
        [FromQuery] int? width = null,
        [FromQuery] int? height = null,
        [FromQuery] int fps = 15,
        CancellationToken cancellationToken = default)
    {
        try
        {
            Response.ContentType = "multipart/x-mixed-replace; boundary=--frame";
            Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            Response.Headers.Append("Pragma", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            var frameDelay = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, Math.Min(fps, 15)));
            byte[]? lastValidFrame = null;
            var consecutiveErrors = 0;
            const int maxConsecutiveErrors = 10;

            _logger.LogInformation("MJPEG stream started for camera {CameraId} at {Width}x{Height} @ {Fps} FPS",
                cameraId, width?.ToString() ?? "native", height?.ToString() ?? "native", fps);

            while (!cancellationToken.IsCancellationRequested && consecutiveErrors < maxConsecutiveErrors)
            {
                try
                {
                    var frameStart = DateTime.UtcNow;

                    // Check cache first
                    if (_frameCache.TryGetValue(cameraId, out var cached) &&
                        DateTime.UtcNow - cached.Timestamp < _cacheLifetime)
                    {
                        await SendFrameAsync(cached.Frame, cancellationToken);
                    }
                    else
                    {
                        var result = await _protectService.GetSnapshotAsync(cameraId, width, height);
                        if (result != null)
                        {
                            lastValidFrame = result.Value.Data;
                            consecutiveErrors = 0;
                            _frameCache[cameraId] = (lastValidFrame, DateTime.UtcNow);
                            await SendFrameAsync(lastValidFrame, cancellationToken);
                        }
                        else
                        {
                            consecutiveErrors++;
                            if (lastValidFrame != null)
                                await SendFrameAsync(lastValidFrame, cancellationToken);
                        }
                    }

                    var elapsed = DateTime.UtcNow - frameStart;
                    var remaining = frameDelay - elapsed;
                    if (remaining > TimeSpan.Zero)
                        await Task.Delay(remaining, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    consecutiveErrors++;
                    _logger.LogDebug(ex, "Error during MJPEG frame fetch for camera {CameraId}", cameraId);
                    if (lastValidFrame != null)
                    {
                        try { await SendFrameAsync(lastValidFrame, cancellationToken); } catch { }
                    }
                    await Task.Delay(frameDelay, cancellationToken);
                }
            }

            _logger.LogInformation("MJPEG stream ended for camera {CameraId}", cameraId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in MJPEG stream for camera {CameraId}", cameraId);
        }
    }

    private async Task SendFrameAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        await Response.WriteAsync("--frame\r\n", cancellationToken);
        await Response.WriteAsync($"Content-Type: image/jpeg\r\n", cancellationToken);
        await Response.WriteAsync($"Content-Length: {imageBytes.Length}\r\n\r\n", cancellationToken);
        await Response.Body.WriteAsync(imageBytes, 0, imageBytes.Length, cancellationToken);
        await Response.WriteAsync("\r\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

}
