## [2.0.2](https://github.com/Bdaya-Dev/Bdaya.Abp.EventBus.PubSub/compare/v2.0.1...v2.0.2) (2026-02-22)


### Bug Fixes

* **ci:** pin .NET SDK via global.json for deterministic builds ([929c401](https://github.com/Bdaya-Dev/Bdaya.Abp.EventBus.PubSub/commit/929c401370b71b4fec05ab01c3ba0ba94052755d))

## [2.0.1](https://github.com/Bdaya-Dev/Bdaya.Abp.EventBus.PubSub/compare/v2.0.0...v2.0.1) (2026-02-22)


### Bug Fixes

* **ci:** restore Release assets before no-restore build ([971f911](https://github.com/Bdaya-Dev/Bdaya.Abp.EventBus.PubSub/commit/971f9110894807170b038f00f32ec35c0ffe8535))

# [2.0.0](https://github.com/Bdaya-Dev/Bdaya.Abp.EventBus.PubSub/compare/v1.1.0...v2.0.0) (2026-02-22)


* feat!: migrate EventBus PubSub to .NET 10 SDK, CPM, xUnit v3, and secure credential APIs ([110f966](https://github.com/Bdaya-Dev/Bdaya.Abp.EventBus.PubSub/commit/110f9669c22bea3458f7055ac190e8a37dc3982d))


### BREAKING CHANGES

* This release migrates test infrastructure to xUnit v3 and updates credential configuration behavior to use explicit GoogleCredential assignment instead of deprecated path-based APIs. Build/CI now require .NET 10 SDK while libraries continue targeting net9.0.

# [1.1.0](https://github.com/Bdaya-Dev/Bdaya.Abp.EventBus.PubSub/compare/v1.0.0...v1.1.0) (2026-01-26)


### Features

* enhance authentication options in Pub/Sub configuration ([b6f5778](https://github.com/Bdaya-Dev/Bdaya.Abp.EventBus.PubSub/commit/b6f577837c671b6c3d3d6ce52ab09a35dcb53fbb))

# 1.0.0 (2026-01-26)


### Bug Fixes

* use proper Task return instead of async void pattern ([1b844db](https://github.com/Bdaya-Dev/Bdaya.Abp.EventBus.PubSub/commit/1b844dbf6e8a8ece023ac6810765a8c7a70cc73b))


### Features

* add semantic-release for automated versioning and publishing ([ba1644d](https://github.com/Bdaya-Dev/Bdaya.Abp.EventBus.PubSub/commit/ba1644df6fa0f3c8b81ff66808d6241ca9b6f025))
