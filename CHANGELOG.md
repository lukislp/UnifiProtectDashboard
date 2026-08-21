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
