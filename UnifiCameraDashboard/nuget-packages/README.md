# Local NuGet feed for NotifyHub

This directory is a flat-folder NuGet feed (see `nuget.config` at the repo root) holding the
`NotifyHub` package used for Web Push notifications (`BackgroundServices/DailyDigestService.cs`,
`Controllers/PushController.cs`). NotifyHub (github.com/lukislp/NotifyHub) is a separate repo and
isn't published to nuget.org - its GitHub Releases attach real versioned nupkgs instead.

The Docker image downloads it automatically at build time from NotifyHub's GitHub Release - no
manual setup is needed when running in a container.

## Local Development

```powershell
Invoke-WebRequest -Uri "https://github.com/lukislp/NotifyHub/releases/download/v0.2.2/NotifyHub.0.2.2.nupkg" -OutFile "UnifiCameraDashboard\nuget-packages\NotifyHub.0.2.2.nupkg"
```

Then `dotnet restore` picks it up via the `local-notifyhub` source in the repo root's
`nuget.config`.

To bump the version: download the new release's nupkg into this folder, delete the old one, and
update the `<PackageReference Include="NotifyHub" Version="..." />` in
`UnifiCameraDashboard.csproj` to match.

This binary is not committed to the repository (see `.gitignore`) - each developer downloads it
locally, and the Docker image downloads it at build time.
