# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["UnifiCameraDashboard.csproj", "nuget.config", "./"]

# NotifyHub (github.com/lukislp/NotifyHub) isn't published to nuget.org - its GitHub Releases
# attach real versioned nupkgs instead. Downloaded into the local feed nuget.config points at,
# before restore needs it - same not-committed-to-git treatment as the YOLO model below, just
# earlier in the build since this one is a compile-time dependency.
RUN mkdir -p nuget-packages && \
    curl -fsSL -o nuget-packages/NotifyHub.0.2.2.nupkg https://github.com/lukislp/NotifyHub/releases/download/v0.2.2/NotifyHub.0.2.2.nupkg

RUN dotnet restore "UnifiCameraDashboard.csproj"

COPY . .
# No --no-restore here: the restore above ran before wwwroot existed in the build context
# (it was copied in afterwards, for Docker layer caching), so its static web assets manifest
# never saw any of this project's own static files - publishing with --no-restore against that
# stale manifest silently drops the entire wwwroot output, including the Blazor framework
# scripts (_framework/blazor.web.js), breaking all server-side interactivity at runtime.
RUN dotnet publish "UnifiCameraDashboard.csproj" -c Release -o /app/publish

# ---- Runtime Stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install FFmpeg via apt (replaces the bundled Windows binaries)
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg locales curl \
    && locale-gen de_DE.UTF-8 \
    && rm -rf /var/lib/apt/lists/*

# Set a UTF-8 locale so umlauts render correctly instead of "?"
ENV LANG=de_DE.UTF-8
ENV LC_ALL=de_DE.UTF-8
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/publish .

# Download the YOLO event-classification model (large binary, not committed to git - matches the
# ffmpeg treatment above; see Tools/yolo/README.md). Hosted as a GitHub Release asset on this repo
# under a fixed "models" tag, deliberately not tied to this app's own semantic-release version
# tags, so the URL never changes across releases.
RUN mkdir -p Tools/yolo && \
    curl -fsSL -o Tools/yolo/yolo11n.onnx https://github.com/lukislp/UnifiProtectDashboard/releases/download/models/yolo11n.onnx

# Data directory for the SQLite database
ENV DATA_DIR=/data
ENV ASPNETCORE_URLS=http://+:5003
ENV ASPNETCORE_ENVIRONMENT=Production

VOLUME ["/data"]
EXPOSE 5003

ENTRYPOINT ["dotnet", "UnifiCameraDashboard.dll"]
