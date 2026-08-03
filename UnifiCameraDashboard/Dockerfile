# ---- Build Stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["UnifiCameraDashboard.csproj", "./"]
RUN dotnet restore "UnifiCameraDashboard.csproj"

COPY . .
RUN dotnet publish "UnifiCameraDashboard.csproj" -c Release -o /app/publish --no-restore

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
