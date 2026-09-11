using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using UnifiCameraDashboard.Controllers;
using UnifiCameraDashboard.Models;

namespace UnifiCameraDashboard.Tests.Controllers;

public class DiscoveryControllerTests
{
    private const string Password = "s3cretPass";

    [Fact]
    public async Task StartDiscovery_DoesNotExposeRtspUrlOrCredentials()
    {
        var camera = new UnifiCamera
        {
            Id = "cam-1",
            Name = "Driveway",
            RtspUrl = $"rtsp://admin:{Password}@192.168.1.10:7447/cam-1?channel=0"
        };
        var controller = new DiscoveryController(new FakeCameraService(camera), NullLogger<DiscoveryController>.Instance);

        var result = await controller.StartDiscovery();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);

        Assert.DoesNotContain("rtsp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, json);
        Assert.Contains("\"Id\":\"cam-1\"", json);
    }
}
