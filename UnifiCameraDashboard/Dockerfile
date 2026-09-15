# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d AS build
WORKDIR /src

COPY ["UnifiCameraDashboard.csproj", "packages.lock.json", "nuget.config", "./"]
# NotifyHub (github.com/lukislp/NotifyHub) isn't published to nuget.org, so it comes from the
# committed flat-folder feed nuget.config points at - no download step before restore.
COPY ["nuget-packages/", "nuget-packages/"]

# --locked-mode: the restore must match the committed packages.lock.json exactly.
# The lock-file properties live in the repo-root Directory.Build.props, which is outside this
# build context (the project folder); the explicit flags here keep the image build locked too.
RUN dotnet restore "UnifiCameraDashboard.csproj" --locked-mode

COPY . .
# No --no-restore here: the restore above ran before wwwroot existed in the build context
# (it was copied in afterwards, for Docker layer caching), so its static web assets manifest
# never saw any of this project's own static files - publishing with --no-restore against that
# stale manifest silently drops the entire wwwroot output, including the Blazor framework
# scripts (_framework/blazor.web.js), breaking all server-side interactivity at runtime.
RUN dotnet publish "UnifiCameraDashboard.csproj" -c Release -o /app/publish -p:RestoreLockedMode=true

# ---- Runtime Stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c AS final
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
# Checksum-verified because that fixed tag is mutable by design: a release asset can be replaced
# in place under the same URL, and an .onnx file is not inert data - ONNX Runtime executes the
# graph it describes, so whoever can overwrite the asset decides what runs inside this container.
# Neither the Trivy scan nor the cosign signature over the image catches that: both only attest
# whatever bytes this build happened to pull. sha256sum -c fails the build on any byte change;
# when the model is intentionally replaced, recompute the sum and update it here together with
# the copy in Tools/yolo/README.md.
RUN mkdir -p Tools/yolo && \
    curl -fsSL -o Tools/yolo/yolo11n.onnx https://github.com/lukislp/UnifiProtectDashboard/releases/download/models/yolo11n.onnx && \
    echo "e538014522f27bd86cfb35106a87fe55c00ccc17e4556733bc5f7093427389aa  Tools/yolo/yolo11n.onnx" | sha256sum -c -

# Data directory for the SQLite database
ENV DATA_DIR=/data
ENV ASPNETCORE_URLS=http://+:5003
ENV ASPNETCORE_ENVIRONMENT=Production

VOLUME ["/data"]
EXPOSE 5003

# Non-root runtime: the aspnet base image ships a dedicated "app" user and
# exposes its numeric UID as $APP_UID (1654) - numeric so runAsNonRoot can be
# verified. /data is created app-owned IN the image so a fresh anonymous or
# named volume inherits that ownership on first mount (plain `docker run` and
# docker-compose work with no extra step - the CI smoke check runs exactly
# that). Kubernetes PVCs don't inherit image ownership, which is what the
# Deployment's chown initContainer is for. DataProtection keys land in the
# app user's own home.
RUN mkdir -p /data && chown $APP_UID:$APP_UID /data
USER $APP_UID

ENTRYPOINT ["dotnet", "UnifiCameraDashboard.dll"]
