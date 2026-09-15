## [1.14.19](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.18...v1.14.19) (2026-09-15)


### Bug Fixes

* **deps:** Bump coverlet.collector from 6.0.4 to 10.0.1 ([#73](https://github.com/lukislp/UnifiProtectDashboard/issues/73)) ([0bc5f0d](https://github.com/lukislp/UnifiProtectDashboard/commit/0bc5f0d7996f4385e982dd6d6793b330dc86e192))
* **deps:** Bump the dotnet group with 4 updates ([#71](https://github.com/lukislp/UnifiProtectDashboard/issues/71)) ([8939a55](https://github.com/lukislp/UnifiProtectDashboard/commit/8939a5556598a1bdbe02183a5d3eb65889cdd387))

## [1.14.18](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.17...v1.14.18) (2026-09-15)


### Bug Fixes

* **deps:** Bump SkiaSharp from 4.151.1 to 4.152.0 ([2f64e94](https://github.com/lukislp/UnifiProtectDashboard/commit/2f64e9448ed9d35bb5b73c9a12b77e06d075c3ee))
* **deps:** Bump SkiaSharp.NativeAssets.Linux from 4.151.1 to 4.152.0 ([6a64fe3](https://github.com/lukislp/UnifiProtectDashboard/commit/6a64fe38539d1e9146d8dfdd131565e4ecf1cbf8))

## [1.14.17](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.16...v1.14.17) (2026-09-15)


### Bug Fixes

* **ci:** bump lukislp/ci-workflows/.github/actions/deploy-key-push ([a7465fd](https://github.com/lukislp/UnifiProtectDashboard/commit/a7465fd9b0589e5d4900244d5b318a4ea3126736))

## [1.14.16](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.15...v1.14.16) (2026-09-15)


### Bug Fixes

* **ci:** bump lukislp/ci-workflows/.github/actions/nuget-severity-gate ([a78509d](https://github.com/lukislp/UnifiProtectDashboard/commit/a78509dc7ad92723750d0783b160259a6c6a8534))
* **ci:** bump lukislp/ci-workflows/.github/workflows/dependabot-auto-merge.yml ([0cfdaad](https://github.com/lukislp/UnifiProtectDashboard/commit/0cfdaad2fd68829a15a009c7f3503e92c2e08679))

## [1.14.15](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.14...v1.14.15) (2026-09-15)


### Bug Fixes

* **build:** commit the NotifyHub package so restore works from a bare checkout ([#67](https://github.com/lukislp/UnifiProtectDashboard/issues/67)) ([3c29c7b](https://github.com/lukislp/UnifiProtectDashboard/commit/3c29c7b8433bcad8e2c33059d3385cd8ed11c533))

## [1.14.14](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.13...v1.14.14) (2026-09-15)


### Bug Fixes

* **docker:** verify the YOLO model checksum at build time ([#66](https://github.com/lukislp/UnifiProtectDashboard/issues/66)) ([6d6e490](https://github.com/lukislp/UnifiProtectDashboard/commit/6d6e490b4b225c47fd4418e909f0ca341e0639db))

## [1.14.13](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.12...v1.14.13) (2026-09-13)


### Bug Fixes

* **k8s:** give probes a 5s timeout so load spikes stop killing pods ([#59](https://github.com/lukislp/UnifiProtectDashboard/issues/59)) ([9d040c1](https://github.com/lukislp/UnifiProtectDashboard/commit/9d040c148ced972e236d7f7d935a4609977bb0a2))

## [1.14.12](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.11...v1.14.12) (2026-09-13)


### Bug Fixes

* **k8s:** add explicit egress policies for the dashboard ([#57](https://github.com/lukislp/UnifiProtectDashboard/issues/57)) ([08f95e7](https://github.com/lukislp/UnifiProtectDashboard/commit/08f95e72f3152fd4630d97f09e750b3ddcb163c5))
* **k8s:** drop the catch-all egress rule from allow-dns ([#58](https://github.com/lukislp/UnifiProtectDashboard/issues/58)) ([d0d98b5](https://github.com/lukislp/UnifiProtectDashboard/commit/d0d98b597d1ad92bd4abecbd16c4b215005caa0c))

## [1.14.11](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.10...v1.14.11) (2026-09-13)


### Bug Fixes

* **k8s:** read-only root filesystem for unifiprotectdashboard ([#56](https://github.com/lukislp/UnifiProtectDashboard/issues/56)) ([afae6d2](https://github.com/lukislp/UnifiProtectDashboard/commit/afae6d27b1d3c26d0a7a86db419c3c9809e66293))

## [1.14.10](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.9...v1.14.10) (2026-09-13)


### Bug Fixes

* **protect:** a zlib-rejected deflated frame is a malformed frame, not a crash ([#53](https://github.com/lukislp/UnifiProtectDashboard/issues/53)) ([b5c25e3](https://github.com/lukislp/UnifiProtectDashboard/commit/b5c25e374f4e344d32ce11554931df728c0af7ac))

## [1.14.9](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.8...v1.14.9) (2026-09-12)


### Bug Fixes

* **ci:** put deploy-bump in the same release-chain concurrency group as the other jobs ([#47](https://github.com/lukislp/UnifiProtectDashboard/issues/47)) ([f017320](https://github.com/lukislp/UnifiProtectDashboard/commit/f0173207dbc9b3866341c480517b9bf7b5856426))

## [1.14.8](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.7...v1.14.8) (2026-09-12)


### Bug Fixes

* **protect:** report broken JSON and bad zlib payloads as malformed frames ([#44](https://github.com/lukislp/UnifiProtectDashboard/issues/44)) ([849fdde](https://github.com/lukislp/UnifiProtectDashboard/commit/849fddee398cc09291dc7d8dd28b0cb711271885))
* **protect:** treat a non-object JSON action payload as a malformed frame ([#45](https://github.com/lukislp/UnifiProtectDashboard/issues/45)) ([b252285](https://github.com/lukislp/UnifiProtectDashboard/commit/b252285959c1914411921d41809ceb74495a0d16))

## [1.14.7](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.6...v1.14.7) (2026-09-12)


### Bug Fixes

* **ci:** bump the deployment image tag from the pipeline instead of Flux ([#38](https://github.com/lukislp/UnifiProtectDashboard/issues/38)) ([a98f935](https://github.com/lukislp/UnifiProtectDashboard/commit/a98f9359077dead0141fa6ca3a326ef9177df7a6))

## [1.14.6](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.5...v1.14.6) (2026-09-11)


### Bug Fixes

* **api:** stop returning RTSP credentials and server paths to the browser ([#35](https://github.com/lukislp/UnifiProtectDashboard/issues/35)) ([7ec44ed](https://github.com/lukislp/UnifiProtectDashboard/commit/7ec44ed37514ff907d17b438814d7d379b0d6315))

## [1.14.5](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.4...v1.14.5) (2026-09-11)


### Bug Fixes

* **ci:** read-only GITHUB_TOKEN in the Dependabot auto-merge workflow ([788c34a](https://github.com/lukislp/UnifiProtectDashboard/commit/788c34ad61a90473997a18df939264ef60ac04e0))

## [1.14.4](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.3...v1.14.4) (2026-09-11)


### Bug Fixes

* resolve CodeQL findings ([#33](https://github.com/lukislp/UnifiProtectDashboard/issues/33)) ([838df09](https://github.com/lukislp/UnifiProtectDashboard/commit/838df09ca9a090d74abd04048ca7a0db8273da22))

## [1.14.3](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.2...v1.14.3) (2026-09-11)


### Bug Fixes

* **ci:** push release commits as a deploy key so the default branch can be ruleset-protected ([8a4d5e3](https://github.com/lukislp/UnifiProtectDashboard/commit/8a4d5e3f50c5e193bf25ff0a406aa4202387aef2))

## [1.14.2](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.1...v1.14.2) (2026-09-04)


### Bug Fixes

* **ci:** bump aquasecurity/trivy-action ([1b268e1](https://github.com/lukislp/UnifiProtectDashboard/commit/1b268e142ab5ed015b755b118c373d24ff0aa6e0))
* **ci:** bump docker/setup-buildx-action from 4.2.0 to 4.3.0 ([ca0e76e](https://github.com/lukislp/UnifiProtectDashboard/commit/ca0e76e908ae4eb978ca3c1b5750e08103346e9a))

## [1.14.1](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.14.0...v1.14.1) (2026-09-03)


### Bug Fixes

* **ci:** add Dependabot for github-actions, nuget, docker ([cc9576c](https://github.com/lukislp/UnifiProtectDashboard/commit/cc9576c3ea2919d8e50fce93ead3c8c2b3172051))

# [1.14.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.13.3...v1.14.0) (2026-08-25)


### Features

* move data volume to Longhorn RWX for cross-node replication ([55c9f33](https://github.com/lukislp/UnifiProtectDashboard/commit/55c9f33f3dfd7a3dc355b4343f875330590b7e1a))

## [1.13.3](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.13.2...v1.13.3) (2026-08-24)


### Bug Fixes

* **k8s:** opt the data volume into the nightly Velero backup ([09c5c8b](https://github.com/lukislp/UnifiProtectDashboard/commit/09c5c8b15ff32228c02d644e0944443c49de43c7))

## [1.13.2](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.13.1...v1.13.2) (2026-08-24)


### Bug Fixes

* create /data app-owned in the image ([83f4366](https://github.com/lukislp/UnifiProtectDashboard/commit/83f436601adc98a0d0c5089d27cfeaf056713337))

## [1.13.1](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.13.0...v1.13.1) (2026-08-24)


### Bug Fixes

* run as non-root ([7d23597](https://github.com/lukislp/UnifiProtectDashboard/commit/7d2359710e6918501b5c975da4e7a027c9202978))

# [1.13.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.12.0...v1.13.0) (2026-08-22)


### Features

* own this repo's Flux GitOps wiring ([770f717](https://github.com/lukislp/UnifiProtectDashboard/commit/770f7172b7adb0a784ba173396e6ca96f3a1ccff))

# [1.12.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.11.0...v1.12.0) (2026-08-22)


### Features

* **ui:** add a Settings link to the main dashboard header ([ed64854](https://github.com/lukislp/UnifiProtectDashboard/commit/ed64854e82c244ffd3849797d413f76ebdf10645))

# [1.11.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.10.0...v1.11.0) (2026-08-22)


### Bug Fixes

* **ci:** restore NotifyHub in test-build/test-lint/test-security ([c243afd](https://github.com/lukislp/UnifiProtectDashboard/commit/c243afdfb9f8efd477fe6e70ce868819bbec3562))


### Features

* **notifications:** add S3 daily digest via Web Push (NotifyHub) ([92016ec](https://github.com/lukislp/UnifiProtectDashboard/commit/92016ec34e6f31166d2f7cf9519de46e322b2bc6))

# [1.10.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.9.0...v1.10.0) (2026-08-22)


### Features

* **cameras:** add the ability to remove a camera from the dashboard ([49862c8](https://github.com/lukislp/UnifiProtectDashboard/commit/49862c85a7ab1785365030a78deff30a4c46a2a9))

# [1.9.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.8.2...v1.9.0) (2026-08-22)


### Features

* **k8s:** rolling updates with an app-level write lock instead of Recreate ([085367c](https://github.com/lukislp/UnifiProtectDashboard/commit/085367c91e1fd1d86fb67b892e6b134aaaa2b2c6))

## [1.8.2](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.8.1...v1.8.2) (2026-08-21)


### Bug Fixes

* **ci:** re-trigger a release to measure the warm-cache arm64 build time ([492a362](https://github.com/lukislp/UnifiProtectDashboard/commit/492a362a77e10760de16902b46bb91fc2d4fff54))

## [1.8.1](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.8.0...v1.8.1) (2026-08-21)


### Bug Fixes

* **ci:** re-trigger a release to validate the new native arm64 pipeline ([f3eefff](https://github.com/lukislp/UnifiProtectDashboard/commit/f3eefff18379043c0ed76cfbf1fcaa9bd5f40570))

# [1.8.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.7.2...v1.8.0) (2026-08-21)


### Features

* **k8s:** make the app deployment Flux-managed ([b36ceba](https://github.com/lukislp/UnifiProtectDashboard/commit/b36ceba7ec8ca1690a28cea3feba77b74f6f5fd1))


### Performance Improvements

* **ci:** build arm64 natively instead of under QEMU emulation ([8daa0fa](https://github.com/lukislp/UnifiProtectDashboard/commit/8daa0fa8d563f0200b1a2a4e2df16baefbb22f80))

## [1.7.2](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.7.1...v1.7.2) (2026-08-21)


### Bug Fixes

* **k8s:** bump S2 measurement deployment to 1.7.1 ([ba609dc](https://github.com/lukislp/UnifiProtectDashboard/commit/ba609dc5975ccd6ad6a1aacefc8f481a91dd491c))

## [1.7.1](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.7.0...v1.7.1) (2026-08-21)


### Bug Fixes

* **classification:** recover events dropped before the queue was unbounded ([1ae52dc](https://github.com/lukislp/UnifiProtectDashboard/commit/1ae52dc148ceea50de0bb125a1a3bfd1af0f71d2))

# [1.7.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.6.2...v1.7.0) (2026-08-21)


### Features

* **classification:** make the classification queue unbounded ([d5ae248](https://github.com/lukislp/UnifiProtectDashboard/commit/d5ae248aa3f004b4ead4b1ed78d7893e3e2c365a))

## [1.6.2](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.6.1...v1.6.2) (2026-08-21)


### Bug Fixes

* **k8s:** bump S2 measurement deployment to 1.6.1 ([06636d6](https://github.com/lukislp/UnifiProtectDashboard/commit/06636d6b3a0fa278a1b13596cd8b4d038697da16)), closes [#7](https://github.com/lukislp/UnifiProtectDashboard/issues/7)

## [1.6.1](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.6.0...v1.6.1) (2026-08-21)


### Bug Fixes

* **events:** stop backfill from silently skipping windows it never covered ([3aa20eb](https://github.com/lukislp/UnifiProtectDashboard/commit/3aa20ebb95ce7c274fa6c7c776e7278d8ae22012))

# [1.6.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.5.0...v1.6.0) (2026-08-21)


### Features

* **k8s:** add HTTPRoute, network policies, and namespace ServiceAccount ([e9b4f72](https://github.com/lukislp/UnifiProtectDashboard/commit/e9b4f72ed5e75309e038020278dc18ef818cd67e))
* **k8s:** add temporary manifests for the S2 measurement deployment ([49e6486](https://github.com/lukislp/UnifiProtectDashboard/commit/49e648655ce12cb7683604a84b5e5b8698689aae)), closes [#5](https://github.com/lukislp/UnifiProtectDashboard/issues/5)

# [1.5.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.4.0...v1.5.0) (2026-08-21)


### Features

* **classification:** add schema and repository support for YOLO labels ([f475b80](https://github.com/lukislp/UnifiProtectDashboard/commit/f475b80ad56c4f9d8411cbf8d514991674f2d133))
* **classification:** add YOLO11n inference pipeline ([b13b3f1](https://github.com/lukislp/UnifiProtectDashboard/commit/b13b3f14041bf838ce98a403b7b8930cf925ef21))
* **ui:** show and filter events by YOLO label ([b419b01](https://github.com/lukislp/UnifiProtectDashboard/commit/b419b01a24444d9972700a1f54bd44b453438480))

# [1.4.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.3.0...v1.4.0) (2026-08-21)


### Features

* **ui:** add chronological events page ([fff6acf](https://github.com/lukislp/UnifiProtectDashboard/commit/fff6acf8a7f89650e8eb75e0ca3af6b53a059bb3))

# [1.3.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.2.0...v1.3.0) (2026-08-21)


### Features

* **events:** add event schema and repository ([2832cca](https://github.com/lukislp/UnifiProtectDashboard/commit/2832cca10b5deed54f7735c721e7ba415941775a))
* **events:** ingest UniFi Protect events via the realtime websocket ([0df18fc](https://github.com/lukislp/UnifiProtectDashboard/commit/0df18fc9fe9e9697980d26465de2ace5dfa97209))

# [1.2.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.1.0...v1.2.0) (2026-08-21)


### Features

* **protect:** decode the UniFi Protect realtime updates websocket protocol ([76b53ff](https://github.com/lukislp/UnifiProtectDashboard/commit/76b53ffd0cfc2dccb151a287fce7e51774370a37))

# [1.1.0](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.0.3...v1.1.0) (2026-08-21)


### Features

* **db:** switch to EF Core migrations with legacy-database bootstrap ([11ba731](https://github.com/lukislp/UnifiProtectDashboard/commit/11ba731298bc3809ebcd615ea2ced5d5f2aee09c))

## [1.0.3](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.0.2...v1.0.3) (2026-08-07)


### Bug Fixes

* add a first-run setup wizard screenshot to the README ([5b7b5ed](https://github.com/lukislp/UnifiProtectDashboard/commit/5b7b5ed33fac3cf569e3544e0b67227801eacf2e))

## [1.0.2](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.0.1...v1.0.2) (2026-08-05)


### Bug Fixes

* surface build/release/license status via README badges ([7388438](https://github.com/lukislp/UnifiProtectDashboard/commit/73884384f69bff33486a0933e1a9803d15678c96))

## [1.0.1](https://github.com/lukislp/UnifiProtectDashboard/compare/v1.0.0...v1.0.1) (2026-08-04)


### Bug Fixes

* Dockerfile silently dropped wwwroot, breaking all interactivity ([62779be](https://github.com/lukislp/UnifiProtectDashboard/commit/62779be1a7868c9141e41c978ec5118d78fed97a))

# 1.0.0 (2026-08-04)


### Bug Fixes

* force consistent CRLF checkout for .cs/.razor files ([b1e3425](https://github.com/lukislp/UnifiProtectDashboard/commit/b1e3425712ce4f5060447c42ce44744526b8b85f))


### Features

* add GitHub Actions CI/CD pipeline ([8f480bc](https://github.com/lukislp/UnifiProtectDashboard/commit/8f480bc6c2f8668dbf4a07010a54b1c0e408423e))

# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

**Note:** This project is under active development. Features and APIs may change.
