using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using UnifiCameraDashboard.Controllers;
using UnifiCameraDashboard.Models;

namespace UnifiCameraDashboard.Tests.Controllers;

public class CamerasControllerTests
{
    private const string Password = "s3cretPass";

    private static UnifiCamera Camera() => new()
    {
        Id = "cam-1",
        Name = "Driveway",
        Model = "G4 Pro",
        RtspUrl = $"rtsp://admin:{Password}@192.168.1.10:7447/cam-1?channel=0",
        IsOnline = true
    };

    private static CamerasController CreateController(params UnifiCamera[] cameras) =>
        new(new FakeCameraService(cameras), NullLogger<CamerasController>.Instance);

    [Fact]
    public async Task GetCameras_DoesNotExposeRtspUrlOrCredentials()
    {
        var result = await CreateController(Camera()).GetCameras();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);

        Assert.DoesNotContain("rtsp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, json);
    }

    [Fact]
    public async Task GetCameras_KeepsFieldsUsedByTheDashboard()
    {
        var result = await CreateController(Camera()).GetCameras();

        var ok = Assert.IsType<OkObjectResult>(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var camera = Assert.Single(doc.RootElement.EnumerateArray());

        Assert.Equal("cam-1", camera.GetProperty("id").GetString());
        Assert.Equal("Driveway", camera.GetProperty("name").GetString());
        Assert.Equal("/api/snapshot/cam-1", camera.GetProperty("snapshotUrl").GetString());
        Assert.True(camera.GetProperty("isOnline").GetBoolean());
    }

    [Fact]
    public async Task GetCamera_DoesNotExposeRtspUrlOrCredentials()
    {
        var result = await CreateController(Camera()).GetCamera("cam-1");

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);

        Assert.DoesNotContain("rtsp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, json);
        Assert.Contains("\"id\":\"cam-1\"", json);
    }
}
