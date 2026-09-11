using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using UnifiCameraDashboard.Controllers;
using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Tests.Controllers;

public class HlsControllerTests
{
    private const string Password = "s3cretPass";

    private static HlsController CreateController(FakeFfmpegService? ffmpeg = null) =>
        new(ffmpeg ?? new FakeFfmpegService(), new FakeSettingsService(), new FakeCameraService(), NullLogger<HlsController>.Instance);

    [Fact]
    public void GetStatus_StripsCredentialsFromRtspUrl()
    {
        var ffmpeg = new FakeFfmpegService();
        ffmpeg.Streams["cam-1"] = new StreamInfo
        {
            CameraId = "cam-1",
            RtspUrl = $"rtsp://admin:{Password}@192.168.1.10:7447/cam-1?channel=0",
            PlaylistPath = "/hls/cam-1/stream.m3u8",
            StartTime = DateTime.Now
        };

        var result = CreateController(ffmpeg).GetStatus();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.DoesNotContain(Password, json);

        using var doc = JsonDocument.Parse(json);
        var stream = Assert.Single(doc.RootElement.GetProperty("streams").EnumerateArray());
        Assert.Equal("rtsp://192.168.1.10:7447/cam-1?channel=0", stream.GetProperty("rtspUrl").GetString());
        Assert.Equal("cam-1", stream.GetProperty("cameraId").GetString());
    }

    [Fact]
    public void ServePlaylist_MissingFile_DoesNotRevealServerPath()
    {
        var cameraId = "missing-" + Guid.NewGuid().ToString("N");

        var result = CreateController().ServePlaylist(cameraId);

        AssertNotFoundWithoutPath(result, "Playlist not found");
    }

    [Fact]
    public void ServeSegment_MissingFile_DoesNotRevealServerPath()
    {
        var cameraId = "missing-" + Guid.NewGuid().ToString("N");

        var result = CreateController().ServeSegment(cameraId, "segment0.ts");

        AssertNotFoundWithoutPath(result, "Segment not found");
    }

    private static void AssertNotFoundWithoutPath(IActionResult result, string expectedError)
    {
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(notFound.Value));
        var body = doc.RootElement;

        Assert.Equal(expectedError, body.GetProperty("error").GetString());
        Assert.False(body.TryGetProperty("file", out _));

        var cwd = Directory.GetCurrentDirectory();
        Assert.All(body.EnumerateObject(), property =>
        {
            var value = property.Value.ToString();
            Assert.DoesNotContain("wwwroot", value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(cwd, value, StringComparison.OrdinalIgnoreCase);
        });
    }
}
