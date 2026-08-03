namespace UnifiCameraDashboard.Models;

public class UnifiCamera
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RtspUrl { get; set; } = string.Empty;
    public string SnapshotUrl { get; set; } = string.Empty;
    public bool IsOnline { get; set; } = true;
    public string MacAddress { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string FirmwareVersion { get; set; } = string.Empty;
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
}
