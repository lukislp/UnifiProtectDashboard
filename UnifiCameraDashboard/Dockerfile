# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["UnifiCameraDashboard.csproj", "./"]
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

# Data directory for the SQLite database
ENV DATA_DIR=/data
ENV ASPNETCORE_URLS=http://+:5003
ENV ASPNETCORE_ENVIRONMENT=Production

VOLUME ["/data"]
EXPOSE 5003

ENTRYPOINT ["dotnet", "UnifiCameraDashboard.dll"]
