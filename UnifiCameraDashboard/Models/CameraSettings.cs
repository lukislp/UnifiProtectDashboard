namespace UnifiCameraDashboard.Models;

public class CameraSettings
{
    public string UnifiProtectUrl { get; set; } = "https://192.168.1.1";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int RefreshIntervalSeconds { get; set; } = 30;
    public bool AutoReconnect { get; set; } = true;
    public int ReconnectDelaySeconds { get; set; } = 5;
    public List<CameraConfig> Cameras { get; set; } = new();
}

public class CameraConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RtspUrl { get; set; } = string.Empty;
    public string SnapshotUrl { get; set; } = string.Empty;
    public int GridOrder { get; set; }
    public bool Enabled { get; set; } = true;
}
