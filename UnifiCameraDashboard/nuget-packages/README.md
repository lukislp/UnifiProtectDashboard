# Local NuGet feed for NotifyHub

This directory is a flat-folder NuGet feed (see `nuget.config` here and at the repo root) holding
the `NotifyHub` package used for Web Push notifications (`BackgroundServices/DailyDigestService.cs`,
`Controllers/PushController.cs`). NotifyHub (github.com/lukislp/NotifyHub) is a separate repo and
isn't published to nuget.org - its GitHub Releases attach real versioned nupkgs instead.

## Why the package is committed

The `.nupkg` used to be gitignored and downloaded at build time (a `curl` step in the Dockerfile
and in every CI job). That made a plain `git clone && dotnet restore` fail with NU1101, because
the feed this `nuget.config` points at was empty - and Dependabot does exactly that: it checks the
repository out and resolves the dependency graph, with no way to run a download step first. A
failing restore aborts the whole NuGet update job, which is why this repo received no NuGet update
pull requests at all while its packages fell behind.

Committing the package (~86 KB) makes the feed self-contained: restore works from a bare checkout,
the Dockerfile and the CI jobs need no network fetch, and the exact bytes are reviewable in git
history instead of being re-downloaded from a release asset on every build.

## Upgrading NotifyHub

Dependabot cannot bump this package - it is not on a public feed - so it is a manual step:

```powershell
$version = "0.2.8"
Invoke-WebRequest -Uri "https://github.com/lukislp/NotifyHub/releases/download/v$version/NotifyHub.$version.nupkg" -OutFile "UnifiCameraDashboard\nuget-packages\NotifyHub.$version.nupkg"
Remove-Item "UnifiCameraDashboard\nuget-packages\NotifyHub.<old-version>.nupkg"
```

Then update `<PackageReference Include="NotifyHub" Version="..." />` in
`UnifiCameraDashboard.csproj` to match and run `dotnet restore UnifiCameraDashboard.sln` to
refresh the `packages.lock.json` files. Keep exactly one `NotifyHub.*.nupkg` in this folder so
the feed offers a single version.

Every NotifyHub release also attaches a `.nupkg.sigstore.json` bundle and build provenance; verify
those against the downloaded file before committing it if the release is not your own.

The longer-term fix is publishing NotifyHub to nuget.org (its pipeline is already set up to pack
and only lacks a `NUGET_API_KEY`), after which this folder, both `nuget.config` files and this
README can be deleted outright.
