using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.BackgroundServices;

public class CameraAutoDiscoveryService : BackgroundService
{
    private readonly ILogger<CameraAutoDiscoveryService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _scanInterval = TimeSpan.FromMinutes(10);

    public CameraAutoDiscoveryService(
        ILogger<CameraAutoDiscoveryService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Camera Auto-Discovery Service started (scan every {Minutes} minutes)",
                  _scanInterval.TotalMinutes);

        // Wait 2 minutes after startup to allow the application to initialize
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndAddNewCamerasAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Auto-Discovery scan");
            }

            // Wait until the next scan
            try
            {
                await Task.Delay(_scanInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is shutting down
                break;
            }
        }

        _logger.LogInformation("Camera Auto-Discovery Service stopped");
    }

    private async Task ScanAndAddNewCamerasAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var cameraService = scope.ServiceProvider.GetRequiredService<IUnifiCameraService>();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var cameraRepository = scope.ServiceProvider.GetRequiredService<ICameraRepository>();

        // Check if Auto-Discovery is enabled
        var autoDiscoveryEnabled = await settingsService.GetAutoDiscoveryEnabledAsync();
        if (!autoDiscoveryEnabled)
        {
            _logger.LogDebug("Auto-Discovery is disabled, skipping scan");
            return;
        }

        _logger.LogInformation("Starting Auto-Discovery scan...");

        // Load current cameras from database
        var existingCameras = await cameraRepository.GetAllCamerasAsync();
        var existingCameraIds = existingCameras.Select(c => c.Id).ToHashSet();

        _logger.LogDebug("{Count} cameras currently in database", existingCameras.Count);

        // Discover cameras via Unifi Protect API
        var discoveredCameras = await cameraService.DiscoverCamerasAsync();

        if (!discoveredCameras.Any())
        {
            _logger.LogWarning("No cameras found (API error or no cameras available)");
            return;
        }

        _logger.LogDebug("{Count} cameras received from Unifi Protect API", discoveredCameras.Count);

        // Find new cameras (IDs not yet present in the database)
        var newCameras = discoveredCameras
            .Where(c => !existingCameraIds.Contains(c.Id))
            .ToList();

        if (newCameras.Any())
        {
            _logger.LogInformation("{Count} new camera(s) found:", newCameras.Count);

            foreach (var camera in newCameras)
            {
                _logger.LogInformation("   {Name} ({Model}) - {Status}",
                    camera.Name,
                    camera.Model,
                    camera.IsOnline ? "Online" : "Offline");
            }

            // Only add new cameras, do not overwrite existing ones
            // This prevents interruption of running streams
            await cameraRepository.AddCamerasAsync(newCameras);

            _logger.LogInformation("{Count} new camera(s) added successfully", newCameras.Count);
            _logger.LogInformation("Existing cameras and their streams remain unchanged");
        }
        else
        {
            _logger.LogDebug("No new cameras found, all cameras are already known");
        }

        // Log removed cameras for informational purposes
        var discoveredCameraIds = discoveredCameras.Select(c => c.Id).ToHashSet();
        var removedCameras = existingCameras
            .Where(c => !discoveredCameraIds.Contains(c.Id))
            .ToList();

        if (removedCameras.Any())
        {
            _logger.LogWarning("{Count} camera(s) no longer found in Unifi Protect:", removedCameras.Count);
            foreach (var camera in removedCameras)
            {
                _logger.LogWarning("   {Name} ({Id})", camera.Name, camera.Id);
            }
            _logger.LogInformation("Cameras are kept in the database and will not be deleted automatically");
        }
    }
}
