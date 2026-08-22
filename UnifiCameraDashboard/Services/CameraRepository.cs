using Microsoft.EntityFrameworkCore;
using UnifiCameraDashboard.Data;
using UnifiCameraDashboard.Models;

namespace UnifiCameraDashboard.Services;

public interface ICameraRepository
{
    Task<List<UnifiCamera>> GetAllCamerasAsync();
    Task<UnifiCamera?> GetCameraByIdAsync(string unifiId);
    Task SaveCamerasAsync(List<UnifiCamera> cameras);
    Task AddCamerasAsync(List<UnifiCamera> cameras);
    Task UpdateCameraAsync(UnifiCamera camera);
    Task<bool> HasAnyCamerasAsync();
    Task ClearAllCamerasAsync();

    /// <summary>
    /// Soft-deletes a camera (Enabled=false) - it disappears from the dashboard and the daily
    /// digest, but its historical events are untouched (no FK/cascade to StoredEvent). Discovery
    /// already checks existence against the unfiltered Cameras table, so a removed camera is
    /// never silently re-added by the auto-discovery loop.
    /// </summary>
    Task RemoveCameraAsync(string unifiId);

    /// <summary>Reverses RemoveCameraAsync - sets Enabled back to true.</summary>
    Task RestoreCameraAsync(string unifiId);

    /// <summary>Cameras with Enabled=false - used by Discovery.razor to offer a "Restore" action instead of miscategorizing them as new.</summary>
    Task<List<UnifiCamera>> GetRemovedCamerasAsync();
}

public class CameraRepository : ICameraRepository
{
    private readonly DashboardDbContext _context;
    private readonly ILogger<CameraRepository> _logger;

    public CameraRepository(DashboardDbContext context, ILogger<CameraRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<UnifiCamera>> GetAllCamerasAsync()
    {
        try
        {
            var storedCameras = await _context.Cameras
       .Where(c => c.Enabled)
                   .OrderBy(c => c.Name) // sort alphabetically by name
                .ThenBy(c => c.GridOrder)
                .ToListAsync();

            return storedCameras.Select(MapToUnifiCamera).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cameras from database");
            return new List<UnifiCamera>();
        }
    }

    public async Task<UnifiCamera?> GetCameraByIdAsync(string unifiId)
    {
        try
        {
            var storedCamera = await _context.Cameras
    .FirstOrDefaultAsync(c => c.UnifiId == unifiId);

            return storedCamera != null ? MapToUnifiCamera(storedCamera) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving camera {Id}", unifiId);
            return null;
        }
    }

    public async Task SaveCamerasAsync(List<UnifiCamera> cameras)
    {
        try
        {
            _logger.LogInformation("Saving {Count} cameras to database", cameras.Count);

            foreach (var camera in cameras)
            {
                var existingCamera = await _context.Cameras
           .FirstOrDefaultAsync(c => c.UnifiId == camera.Id);

                if (existingCamera != null)
                {    // Update existing camera
                    existingCamera.Name = camera.Name;
                    existingCamera.SnapshotUrl = camera.SnapshotUrl;
                    existingCamera.RtspUrl = camera.RtspUrl;
                    existingCamera.MacAddress = camera.MacAddress;
                    existingCamera.Model = camera.Model;
                    existingCamera.FirmwareVersion = camera.FirmwareVersion;
                    existingCamera.Width = camera.Width;
                    existingCamera.Height = camera.Height;
                    existingCamera.LastSeen = DateTime.UtcNow;
                    existingCamera.UpdatedAt = DateTime.UtcNow;

                    _logger.LogInformation("Camera updated: {Name} ({Id})", camera.Name, camera.Id);
                }
                else
                {  // Add new camera
                    var maxOrder = await _context.Cameras.MaxAsync(c => (int?)c.GridOrder) ?? 0;

                    var newCamera = new StoredCamera
                    {
                        UnifiId = camera.Id,
                        Name = camera.Name,
                        SnapshotUrl = camera.SnapshotUrl,
                        RtspUrl = camera.RtspUrl,
                        MacAddress = camera.MacAddress,
                        Model = camera.Model,
                        FirmwareVersion = camera.FirmwareVersion,
                        Width = camera.Width,
                        Height = camera.Height,
                        GridOrder = maxOrder + 1,
                        Enabled = true,
                        LastSeen = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Cameras.Add(newCamera);
                    _logger.LogInformation("New camera added: {Name} ({Id})", camera.Name, camera.Id);
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Cameras saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving cameras");
            throw;
        }
    }

    public async Task UpdateCameraAsync(UnifiCamera camera)
    {
        try
        {
            var storedCamera = await _context.Cameras
                        .FirstOrDefaultAsync(c => c.UnifiId == camera.Id);

            if (storedCamera != null)
            {
                storedCamera.Name = camera.Name;
                storedCamera.SnapshotUrl = camera.SnapshotUrl;
                storedCamera.RtspUrl = camera.RtspUrl;
                storedCamera.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating camera {Id}", camera.Id);
            throw;
        }
    }

    public async Task<bool> HasAnyCamerasAsync()
    {
        return await _context.Cameras.AnyAsync();
    }

    public async Task RemoveCameraAsync(string unifiId)
    {
        try
        {
            var storedCamera = await _context.Cameras.FirstOrDefaultAsync(c => c.UnifiId == unifiId);
            if (storedCamera == null)
            {
                return;
            }

            storedCamera.Enabled = false;
            storedCamera.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Camera removed: {Name} ({Id})", storedCamera.Name, unifiId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing camera {Id}", unifiId);
            throw;
        }
    }

    public async Task RestoreCameraAsync(string unifiId)
    {
        try
        {
            var storedCamera = await _context.Cameras.FirstOrDefaultAsync(c => c.UnifiId == unifiId);
            if (storedCamera == null)
            {
                return;
            }

            storedCamera.Enabled = true;
            storedCamera.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Camera restored: {Name} ({Id})", storedCamera.Name, unifiId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring camera {Id}", unifiId);
            throw;
        }
    }

    public async Task<List<UnifiCamera>> GetRemovedCamerasAsync()
    {
        try
        {
            var storedCameras = await _context.Cameras
                .Where(c => !c.Enabled)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return storedCameras.Select(MapToUnifiCamera).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving removed cameras");
            return new List<UnifiCamera>();
        }
    }

    public async Task ClearAllCamerasAsync()
    {
        try
        {
            _context.Cameras.RemoveRange(_context.Cameras);
            await _context.SaveChangesAsync();
            _logger.LogInformation("All cameras deleted from database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all cameras");
            throw;
        }
    }

    public async Task AddCamerasAsync(List<UnifiCamera> cameras)
    {
        try
        {
            _logger.LogInformation("Adding {Count} new camera(s)", cameras.Count);

            var maxOrder = await _context.Cameras.MaxAsync(c => (int?)c.GridOrder) ?? 0;
            var orderCounter = maxOrder;

            foreach (var camera in cameras)
            {
                // Check if camera already exists (safety check)
                var exists = await _context.Cameras.AnyAsync(c => c.UnifiId == camera.Id);
                if (exists)
                {
                    _logger.LogWarning("Camera {Name} ({Id}) already exists, skipping", camera.Name, camera.Id);
                    continue;
                }

                // Add new camera
                orderCounter++;
                var newCamera = new StoredCamera
                {
                    UnifiId = camera.Id,
                    Name = camera.Name,
                    SnapshotUrl = camera.SnapshotUrl,
                    RtspUrl = camera.RtspUrl,
                    MacAddress = camera.MacAddress,
                    Model = camera.Model,
                    FirmwareVersion = camera.FirmwareVersion,
                    Width = camera.Width,
                    Height = camera.Height,
                    GridOrder = orderCounter,
                    Enabled = true,
                    LastSeen = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Cameras.Add(newCamera);
                _logger.LogInformation("New camera added: {Name} ({Id}) - Order: {Order}",
                     camera.Name, camera.Id, orderCounter);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("{Count} new camera(s) successfully saved to DB", cameras.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding new cameras");
            throw;
        }
    }

    private UnifiCamera MapToUnifiCamera(StoredCamera stored)
    {
        return new UnifiCamera
        {
            Id = stored.UnifiId,
            Name = stored.Name,
            SnapshotUrl = stored.SnapshotUrl,
            RtspUrl = stored.RtspUrl,
            MacAddress = stored.MacAddress,
            Model = stored.Model,
            FirmwareVersion = stored.FirmwareVersion,
            Width = stored.Width,
            Height = stored.Height,
            IsOnline = true // immediately overridden by live status from Unifi Protect
        };
    }
}
