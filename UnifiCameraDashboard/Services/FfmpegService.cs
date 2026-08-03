using System.Diagnostics;
using System.Collections.Concurrent;

namespace UnifiCameraDashboard.Services;

public interface IFfmpegService
{
    Task<string?> StartHlsStreamAsync(string cameraId, string rtspUrl);
    void StopHlsStream(string cameraId);
    bool IsStreamActive(string cameraId);
    Dictionary<string, StreamInfo> GetActiveStreams();
}

public class StreamInfo
{
    public string CameraId { get; set; } = "";
    public string RtspUrl { get; set; } = "";
    public string PlaylistPath { get; set; } = "";
    public DateTime StartTime { get; set; }
    public Process? Process { get; set; }
    public bool IsRunning => Process != null && !Process.HasExited;
}

public class FfmpegService : IFfmpegService, IDisposable
{
    private readonly ILogger<FfmpegService> _logger;
    private readonly ConcurrentDictionary<string, StreamInfo> _activeStreams = new();
    private readonly string _outputBaseDir;
    private readonly string _ffmpegPath;
    private readonly SemaphoreSlim _startLock = new(1, 1);

    public FfmpegService(ILogger<FfmpegService> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _outputBaseDir = Path.Combine(env.WebRootPath, "hls");

        _ffmpegPath = GetFfmpegPath();

        Directory.CreateDirectory(_outputBaseDir);
        _logger.LogInformation("HLS output directory: {Path}", _outputBaseDir);

        CleanupOldStreams();
    }

    public async Task<string?> StartHlsStreamAsync(string cameraId, string rtspUrl)
    {
        if (_activeStreams.TryGetValue(cameraId, out var existingStream))
        {
            if (existingStream.IsRunning)
            {
                _logger.LogInformation("HLS stream for camera {CameraId} is already running", cameraId);
                return existingStream.PlaylistPath;
            }
            else
            {
                StopHlsStream(cameraId);
            }
        }

        await _startLock.WaitAsync();
        try
        {
            if (_activeStreams.TryGetValue(cameraId, out existingStream) && existingStream.IsRunning)
            {
                return existingStream.PlaylistPath;
            }

            var outputDir = Path.Combine(_outputBaseDir, cameraId);
            Directory.CreateDirectory(outputDir);

            var playlistFile = Path.Combine(outputDir, "stream.m3u8");
            var segmentPattern = Path.Combine(outputDir, "segment_%03d.ts");

            CleanupCameraDirectory(outputDir);

            if (!Directory.Exists(outputDir))
            {
                _logger.LogError("Output directory could not be created: {Dir}", outputDir);
                return null;
            }

            var testFile = Path.Combine(outputDir, "test.tmp");
            try
            {
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                _logger.LogDebug("Write access to {Dir} confirmed", outputDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No write access to {Dir}", outputDir);
                return null;
            }

            var arguments = BuildFfmpegArguments(rtspUrl, playlistFile, segmentPattern);

            _logger.LogInformation("Starting HLS stream for camera {CameraId}", cameraId);
            _logger.LogDebug("FFmpeg Command: {FFmpegPath} {Args}", _ffmpegPath, arguments);
            _logger.LogDebug("Output Directory: {OutputDir}", outputDir);
            _logger.LogDebug("Playlist File: {File}", playlistFile);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.OutputDataReceived += (sender, e) =>
                      {
                          if (!string.IsNullOrEmpty(e.Data))
                          {
                              _logger.LogInformation("FFmpeg [{CameraId}] Output: {Data}", cameraId, e.Data);
                          }
                      };

            process.ErrorDataReceived += (sender, e) =>
       {
           if (!string.IsNullOrEmpty(e.Data))
           {
               _logger.LogInformation("FFmpeg [{CameraId}] Stderr: {Data}", cameraId, e.Data);
           }
       };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var startTime = DateTime.Now;
            var playlistExists = false;
            var segmentExists = false;

            while ((DateTime.Now - startTime).TotalSeconds < 15)
            {
                await Task.Delay(500);

                if (!playlistExists && File.Exists(playlistFile))
                {
                    playlistExists = true;
                    _logger.LogInformation("Playlist file created: {File}", playlistFile);

                    try
                    {
                        var content = File.ReadAllText(playlistFile);
                        _logger.LogDebug("Playlist Inhalt:\n{Content}", content);
                    }
                    catch { }
                }

                if (!segmentExists && Directory.Exists(outputDir))
                {
                    var segments = Directory.GetFiles(outputDir, "segment_*.ts");
                    if (segments.Length > 0)
                    {
                        segmentExists = true;
                        _logger.LogInformation("First segments created: {Count} files", segments.Length);
                    }
                }

                if (playlistExists && segmentExists)
                {
                    _logger.LogInformation("HLS stream ready for camera {CameraId}", cameraId);
                    break;
                }

                if (process.HasExited)
                {
                    _logger.LogError("FFmpeg process has exited! Exit code: {ExitCode}", process.ExitCode);
                    return null;
                }
            }

            if (!playlistExists)
            {
                _logger.LogError("Playlist file was not created: {File}", playlistFile);
                process.Kill(true);
                return null;
            }

            if (!segmentExists)
            {
                _logger.LogWarning("No segments created, but playlist exists. Stream may start with a delay.");
            }

            var streamInfo = new StreamInfo
            {
                CameraId = cameraId,
                RtspUrl = rtspUrl,
                PlaylistPath = $"/hls/{cameraId}/stream.m3u8",
                StartTime = DateTime.Now,
                Process = process
            };

            _activeStreams[cameraId] = streamInfo;

            _logger.LogInformation("HLS stream started for camera {CameraId}, playlist: {Path}",
           cameraId, streamInfo.PlaylistPath);

            return streamInfo.PlaylistPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting HLS stream for camera {CameraId}", cameraId);
            return null;
        }
        finally
        {
            _startLock.Release();
        }
    }

    private string BuildFfmpegArguments(string rtspUrl, string playlistFile, string segmentPattern)
    {
        return $@"-rtsp_transport tcp " +
               $@"-rtsp_flags prefer_tcp " +
    $@"-allowed_media_types video+audio " +
          $@"-fflags +genpts+discardcorrupt " +
               $@"-use_wallclock_as_timestamps 1 " +
    $@"-timeout 5000000 " +
    $@"-i ""{rtspUrl}"" " +
 $@"-c:v copy " +
   $@"-c:a aac -b:a 128k -ar 44100 " +
           $@"-f hls " +
   $@"-hls_time 2 " +
     $@"-hls_list_size 10 " +
    $@"-hls_flags delete_segments+omit_endlist " +
       $@"-hls_segment_type mpegts " +
   $@"-hls_segment_filename ""{segmentPattern}"" " +
   $@"-start_number 0 " +
               $@"-hls_allow_cache 0 " +
       $@"-loglevel warning " +
            $@"-y " +
    $@"""{playlistFile}""";
    }

    public void StopHlsStream(string cameraId)
    {
        if (_activeStreams.TryRemove(cameraId, out var streamInfo))
        {
            try
            {
                if (streamInfo.Process != null && !streamInfo.Process.HasExited)
                {
                    _logger.LogInformation("Stopping HLS stream for camera {CameraId}", cameraId);
                    streamInfo.Process.Kill(true);
                    streamInfo.Process.WaitForExit(5000);
                    streamInfo.Process.Dispose();
                }

                var outputDir = Path.Combine(_outputBaseDir, cameraId);
                if (Directory.Exists(outputDir))
                {
                    try
                    {
                        Directory.Delete(outputDir, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete directory: {Dir}", outputDir);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping HLS stream for camera {CameraId}", cameraId);
            }
        }
    }

    public bool IsStreamActive(string cameraId)
    {
        return _activeStreams.TryGetValue(cameraId, out var stream) && stream.IsRunning;
    }

    public Dictionary<string, StreamInfo> GetActiveStreams()
    {
        var deadStreams = _activeStreams
    .Where(kvp => !kvp.Value.IsRunning)
    .Select(kvp => kvp.Key)
   .ToList();

        foreach (var deadStream in deadStreams)
        {
            StopHlsStream(deadStream);
        }

        return _activeStreams.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private void CleanupOldStreams()
    {
        try
        {
            if (Directory.Exists(_outputBaseDir))
            {
                var directories = Directory.GetDirectories(_outputBaseDir);
                foreach (var dir in directories)
                {
                    try
                    {
                        Directory.Delete(dir, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete old stream directory: {Dir}", dir);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old streams");
        }
    }

    private void CleanupCameraDirectory(string directory)
    {
        try
        {
            var files = Directory.GetFiles(directory);
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                }
                catch { }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _logger.LogInformation("FFmpeg service shutting down, stopping all streams...");

        foreach (var cameraId in _activeStreams.Keys.ToList())
        {
            StopHlsStream(cameraId);
        }

        _startLock.Dispose();
    }

    private string GetFfmpegPath()
    {
        // On Linux/Docker the binary is called "ffmpeg", on Windows "ffmpeg.exe"
        var isWindows = OperatingSystem.IsWindows();
        var binaryName = isWindows ? "ffmpeg.exe" : "ffmpeg";

        var appDirectory = AppContext.BaseDirectory;
        var bundledPath = Path.Combine(appDirectory, "Tools", "ffmpeg", binaryName);

        _logger.LogDebug("Searching for FFmpeg: App={AppDir}, Bundled={Path}", appDirectory, bundledPath);

        if (File.Exists(bundledPath))
        {
            _logger.LogInformation("FFmpeg found (bundled): {Path}", bundledPath);
            return bundledPath;
        }

        // Fallback: Check content root (for development)
        var contentRoot = Directory.GetCurrentDirectory();
        var devPath = Path.Combine(contentRoot, "Tools", "ffmpeg", binaryName);

        if (File.Exists(devPath))
        {
            _logger.LogInformation("FFmpeg found (dev): {Path}", devPath);
            return devPath;
        }

        // Fallback: Search in system PATH (supports both Windows and Linux)
        try
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var path in pathEnv.Split(Path.PathSeparator))
                {
                    var ffmpegPath = Path.Combine(path, binaryName);
                    if (File.Exists(ffmpegPath))
                    {
                        _logger.LogInformation("FFmpeg found (system): {Path}", ffmpegPath);
                        return ffmpegPath;
                    }
                }
            }
        }
        catch { /* Ignore PATH search errors */ }

        // FFmpeg not found - throw exception
        _logger.LogError("FFmpeg not found. Searched: {Bundled}, {Dev}, System PATH", bundledPath, devPath);

        throw new FileNotFoundException(
            $"FFmpeg not found. Please install ffmpeg or place {binaryName} in Tools/ffmpeg/.",
            binaryName);
    }
}
