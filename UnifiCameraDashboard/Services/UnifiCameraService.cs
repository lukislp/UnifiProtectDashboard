using UnifiCameraDashboard.Models;

namespace UnifiCameraDashboard.Services;

public interface IUnifiCameraService
{
    Task<List<UnifiCamera>> GetCamerasAsync();
    Task<UnifiCamera?> GetCameraByIdAsync(string cameraId);
    Task<bool> TestConnectionAsync();
    Task<string> GetCameraSnapshotAsync(string cameraId);
    Task<List<UnifiCamera>> DiscoverCamerasAsync();
    Task<bool> SaveDiscoveredCamerasAsync(List<UnifiCamera> cameras);
}

public class UnifiCameraService : IUnifiCameraService
{
    private readonly ICameraRepository _cameraRepository;
    private readonly IUnifiProtectService _protectService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<UnifiCameraService> _logger;

    public UnifiCameraService(
   ICameraRepository cameraRepository,
       IUnifiProtectService protectService,
   ISettingsService settingsService,
          ILogger<UnifiCameraService> logger)
    {
        _cameraRepository = cameraRepository;
        _protectService = protectService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<List<UnifiCamera>> GetCamerasAsync()
    {
        try
        {
            // Load cameras from database
            var cameras = await _cameraRepository.GetAllCamerasAsync();

            if (cameras.Any())
            {
                _logger.LogInformation("{Count} cameras loaded from database", cameras.Count);

                // Update IsOnline status from Unifi Protect with retry (important after container restart)
                Dictionary<string, bool>? liveStatus = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        liveStatus = await _protectService.GetCameraStatusAsync();
                        if (liveStatus.Count > 0) break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Live status attempt {Attempt}/3 failed", attempt);
                    }

                    if (attempt < 3)
                        await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                }

                if (liveStatus != null && liveStatus.Count > 0)
                {
                    foreach (var camera in cameras)
                    {
                        camera.IsOnline = liveStatus.ContainsKey(camera.Id) && liveStatus[camera.Id];
                        _logger.LogDebug("Camera {Name} ({Id}): {Status}",
                            camera.Name, camera.Id, camera.IsOnline ? "Online" : "Offline");
                    }
                }
                else
                {
                    _logger.LogWarning("Live status not available after 3 attempts, showing cameras as online");
                    // Optimistic: show all as online until next refresh
                }

                // Convert snapshot URLs to local proxy URLs with resize
                foreach (var camera in cameras)
                {
                    camera.SnapshotUrl = $"/api/snapshot/{camera.Id}?w=640&h=360";
                }

                return cameras;
            }

            // Fallback: try auto-discovery if enabled
            var autoDiscoveryEnabled = await _settingsService.GetAutoDiscoveryEnabledAsync();
            if (autoDiscoveryEnabled)
            {
                _logger.LogInformation("No cameras in DB, starting auto-discovery...");
                var discoveredCameras = await DiscoverCamerasAsync();

                // Deduplicate by ID
                discoveredCameras = discoveredCameras
                    .GroupBy(c => c.Id)
                    .Select(g => g.First())
                    .ToList();

                if (discoveredCameras.Any())
                {
                    await SaveDiscoveredCamerasAsync(discoveredCameras);
                    return discoveredCameras;
                }
            }

            // Letzter Fallback: Demo-Kameras
            _logger.LogWarning("No cameras found, showing demo cameras");
            return GetDemoCameras();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cameras");
            return GetDemoCameras();
        }
    }

    public async Task<UnifiCamera?> GetCameraByIdAsync(string cameraId)
    {
        try
        {
            var cameras = await GetCamerasAsync();
            return cameras.FirstOrDefault(c => c.Id == cameraId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving camera {CameraId}", cameraId);
            return null;
        }
    }

    public async Task<List<UnifiCamera>> DiscoverCamerasAsync()
    {
        try
        {
            _logger.LogInformation("Starting camera discovery...");
            var cameras = await _protectService.DiscoverCamerasAsync();

            if (cameras.Any())
            {
                _logger.LogInformation("{Count} cameras discovered", cameras.Count);
            }
            else
            {
                _logger.LogWarning("No cameras found");
            }

            return cameras;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during discovery");
            return new List<UnifiCamera>();
        }
    }

    public async Task<bool> SaveDiscoveredCamerasAsync(List<UnifiCamera> cameras)
    {
        try
        {
            // Deduplicate by ID before saving
            var distinct = cameras
                .GroupBy(c => c.Id)
                .Select(g => g.First())
                .ToList();

            _logger.LogInformation("Saving {Count} discovered cameras to database", distinct.Count);
            // AddCamerasAsync skips cameras that already exist in the DB
            await _cameraRepository.AddCamerasAsync(distinct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving cameras");
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var url = await _settingsService.GetUnifiProtectUrlAsync();
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            return await _protectService.TestConnectionAsync(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            return false;
        }
    }

    public async Task<string> GetCameraSnapshotAsync(string cameraId)
    {
        // Return the local proxy URL
        return $"/api/snapshot/{cameraId}";
    }

    private List<UnifiCamera> GetDemoCameras()
    {
        // Demo cameras for development/testing
        return new List<UnifiCamera>
        {
   new UnifiCamera
         {
       Id = "demo-1",
 Name = "Eingang (Demo)",
      RtspUrl = "rtsp://demo.example.com/camera1",
 SnapshotUrl = "https://via.placeholder.com/1920x1080/1a1f3a/10b981?text=Eingang+Demo",
        IsOnline = true
     },
 new UnifiCamera
       {
      Id = "demo-2",
    Name = "Garten (Demo)",
     RtspUrl = "rtsp://demo.example.com/camera2",
          SnapshotUrl = "https://via.placeholder.com/1920x1080/1a1f3a/10b981?text=Garten+Demo",
   IsOnline = true
    },
        new UnifiCamera
            {
           Id = "demo-3",
     Name = "Garage (Demo)",
   RtspUrl = "rtsp://demo.example.com/camera3",
SnapshotUrl = "https://via.placeholder.com/1920x1080/1a1f3a/10b981?text=Garage+Demo",
IsOnline = true
   },
   new UnifiCamera
    {
    Id = "demo-4",
   Name = "Office (Demo)",
         RtspUrl = "rtsp://demo.example.com/camera4",
       SnapshotUrl = "https://via.placeholder.com/1920x1080/1a1f3a/10b981?text=Office+Demo",
   IsOnline = true
    }
        };
    }
}
