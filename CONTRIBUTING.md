# Contributing to UnifiProtectDashboard

Thanks for taking the time. UnifiProtectDashboard is a single-maintainer project, so the process is
deliberately small - but it is the same for every change, including the maintainer's own.

## How changes get in

1. Open an issue first for anything bigger than a typo or an obvious bug fix, so the direction can
   be agreed before you spend time on it. Use the templates under `.github/ISSUE_TEMPLATE/`.
2. Fork the repository (or branch, if you have write access) and make your change on a branch.
3. Open a pull request against `main`. The pull-request template asks for what changed and why.
4. `main` is protected: a PR merges only after the test stage of
   [`.github/workflows/ci-cd.yml`](.github/workflows/ci-cd.yml) is green and the branch is up to
   date with `main` (enable auto-merge and it lands on its own once that is the case). Nobody
   pushes to `main` directly, not even the maintainer.

## What a pull request needs

- **Conventional Commits.** The version and the changelog are generated from the commit messages
  (`feat:` = minor release, `fix:` = patch release, `build:`/`ci:`/`docs:`/`test:` = no release).
  Squash-merge keeps the PR title as the commit message, so give the PR a Conventional Commit
  title.
- **Green required checks.** `test-build`, `test-lint`, `test-security` and
  `review / dependency-review` are required; a red one blocks the merge.
- **Tests for new functionality.** New features and bug fixes come with tests in
  `UnifiCameraDashboard.Tests` (it is laid out as `BackgroundServices/`, `Controllers/`,
  `Services/`). A PR that adds behaviour without a test is asked to add one.
- **Formatting and warnings.** `dotnet format UnifiCameraDashboard.sln --verify-no-changes` runs as
  the required `test-lint` check; run `dotnet format UnifiCameraDashboard.sln` before pushing.
  Warnings are errors repo-wide (`TreatWarningsAsErrors` in `Directory.Build.props`) - do not
  silence one without saying why in the PR.
- **Lock files.** Every project carries a `packages.lock.json` and CI restores with
  `--locked-mode`, so a csproj that disagrees with its lock file fails the restore instead of
  silently updating it. A plain `dotnet restore UnifiCameraDashboard.sln` refreshes them locally
  after a package change - commit the result.
- **Vulnerable packages.** `test-security` restores and then fails the build on known High or
  Critical NuGet advisories.
- **Both languages.** The UI ships English and German; a new user-facing string needs an entry in
  both resource sets.

## Running things locally

Requires the .NET 10 SDK and `ffmpeg` on `PATH`.

This project depends on NotifyHub, which is **not published on nuget.org**. Fetch the package into
the local feed before restoring (the same step CI runs, see
`UnifiCameraDashboard/nuget-packages/README.md`):

```bash
mkdir -p UnifiCameraDashboard/nuget-packages
curl -fsSL -o UnifiCameraDashboard/nuget-packages/NotifyHub.0.2.2.nupkg \
  https://github.com/lukislp/NotifyHub/releases/download/v0.2.2/NotifyHub.0.2.2.nupkg
```

Then:

```bash
cd UnifiCameraDashboard
dotnet run
```

This opens `https://localhost:7150`; the setup wizard runs on first start. Override the data
directory with `DATA_DIR=/tmp/mycameras dotnet run`. With Docker, `docker compose up -d`.

The same commands CI runs:

```bash
dotnet restore UnifiCameraDashboard.sln --locked-mode
dotnet build UnifiCameraDashboard.sln --configuration Release --no-restore
dotnet test UnifiCameraDashboard.sln --configuration Release --no-build
dotnet format UnifiCameraDashboard.sln --verify-no-changes
```

## Security issues

Please do not open a public issue for a vulnerability - use the private reporting path described
in [SECURITY.md](SECURITY.md). The [Code of Conduct](CODE_OF_CONDUCT.md) applies to every
interaction in this repository.
