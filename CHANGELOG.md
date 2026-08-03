# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Features

- **HLS live streaming** — H.264 video via FFmpeg + hls.js
- **Snapshot mode** — auto-refreshing JPEG previews, low bandwidth
- **Camera auto-discovery** — scans the UniFi Protect console and persists cameras to SQLite
- **Setup wizard** — guided first-run configuration
- **i18n** — English and German included, browser-locale detection
- **Docker-first deployment** — `docker compose up -d`, FFmpeg installed automatically in the image

### Technical Details

- .NET 10, Blazor Server (InteractiveServer render mode)
- SQLite via EF Core (`EnsureCreatedAsync`, no migrations)
- UniFi Protect cookie + Bearer token session, shared across scoped service instances
- Credentials encrypted at rest (AES) with a locally generated, persisted key

### Security

- No default credentials
- Configuration files with secrets are excluded from version control (see `.gitignore`)
- Passwords are encrypted before being stored in the database

---

**Note:** This project is under active development. Features and APIs may change.
