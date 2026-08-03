# FFmpeg for UniFi Camera Dashboard

This directory optionally holds a local `ffmpeg.exe` used for HLS video streaming on Windows.

The Docker image installs FFmpeg automatically via `apt` — no manual setup is needed when running in a container.

## Local Windows Development

The dashboard looks for FFmpeg in this order:
1. `Tools/ffmpeg/ffmpeg.exe` in this directory
2. The system `PATH`

If `ffmpeg` is already on your `PATH`, you don't need to do anything here.

### Manual Installation

1. Download a build from https://www.gyan.dev/ffmpeg/builds/ (`ffmpeg-release-essentials.zip`)
2. Extract `ffmpeg-X.X.X-essentials_build/bin/ffmpeg.exe`
3. Copy it to `UnifiCameraDashboard/Tools/ffmpeg/ffmpeg.exe`

### Verify

```powershell
.\UnifiCameraDashboard\Tools\ffmpeg\ffmpeg.exe -version
```

## License

FFmpeg is licensed under LGPL 2.1+: https://ffmpeg.org/legal.html

This binary is not committed to the repository (see `.gitignore`) — each developer downloads it locally, and production/Docker builds install it from the distro's package manager.
