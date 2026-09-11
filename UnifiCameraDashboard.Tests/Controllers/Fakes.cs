using UnifiCameraDashboard.Models;
using UnifiCameraDashboard.Services;

namespace UnifiCameraDashboard.Tests.Controllers;

internal sealed class FakeCameraService : IUnifiCameraService
{
    private readonly List<UnifiCamera> _cameras;

    public FakeCameraService(params UnifiCamera[] cameras)
    {
        _cameras = cameras.ToList();
    }

    public Task<List<UnifiCamera>> GetCamerasAsync() => Task.FromResult(_cameras.ToList());

    public Task<UnifiCamera?> GetCameraByIdAsync(string cameraId) =>
        Task.FromResult(_cameras.FirstOrDefault(c => c.Id == cameraId));

    public Task<bool> TestConnectionAsync() => Task.FromResult(true);

    public Task<string> GetCameraSnapshotAsync(string cameraId) => Task.FromResult(string.Empty);

    public Task<List<UnifiCamera>> DiscoverCamerasAsync() => Task.FromResult(_cameras.ToList());

    public Task<bool> SaveDiscoveredCamerasAsync(List<UnifiCamera> cameras) => Task.FromResult(true);
}

internal sealed class FakeFfmpegService : IFfmpegService
{
    public Dictionary<string, StreamInfo> Streams { get; } = new();

    public Task<string?> StartHlsStreamAsync(string cameraId, string rtspUrl)
    {
        Streams[cameraId] = new StreamInfo
        {
            CameraId = cameraId,
            RtspUrl = rtspUrl,
            PlaylistPath = $"/hls/{cameraId}/stream.m3u8",
            StartTime = DateTime.Now
        };
        return Task.FromResult<string?>(Streams[cameraId].PlaylistPath);
    }

    public void StopHlsStream(string cameraId) => Streams.Remove(cameraId);

    public bool IsStreamActive(string cameraId) => Streams.ContainsKey(cameraId);

    public Dictionary<string, StreamInfo> GetActiveStreams() => new(Streams);
}

internal sealed class FakeSettingsService : ISettingsService
{
    public Task<string?> GetSettingAsync(string key) => Task.FromResult<string?>(null);

    public Task SetSettingAsync(string key, string value, bool encrypt = false) => Task.CompletedTask;

    public Task<bool> IsInitialSetupCompleteAsync() => Task.FromResult(true);

    public Task<string?> GetUnifiProtectUrlAsync() => Task.FromResult<string?>("https://nvr.local");

    public Task<string?> GetUsernameAsync() => Task.FromResult<string?>("admin");

    public Task<string?> GetPasswordAsync() => Task.FromResult<string?>("secret");

    public Task<int> GetRefreshIntervalAsync() => Task.FromResult(30);

    public Task<bool> GetAutoDiscoveryEnabledAsync() => Task.FromResult(false);

    public Task SaveUnifiCredentialsAsync(string url, string username, string password) => Task.CompletedTask;

    public Task<bool> GetDailyDigestEnabledAsync() => Task.FromResult(false);

    public Task<string> GetDailyDigestTimeOfDayAsync() => Task.FromResult("07:00");

    public Task SaveDailyDigestSettingsAsync(bool enabled, string timeOfDay) => Task.CompletedTask;
}
