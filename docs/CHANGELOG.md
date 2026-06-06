# Cirreum.Domain Changelog

All notable changes to **Cirreum.Domain** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Changed

- **Code-first cache provider selection.** `AddCirreumCaching` now registers the settings + a no-op
  default only; choose a provider explicitly via `AddInMemoryCacheService()` (or an infrastructure
  package's `Add*CacheService`). A new public `AddCacheService(factory)` helper lets provider packages
  set the active `ICacheService` (with telemetry + keyed consumers), *replacing* any prior registration,
  so it works in any order after `AddCirreumCaching` / `AddDomainServices`. Also removes the internal
  `QueryCachingDiagnostics` misconfiguration warning — obsolete now that there is no provider enum to
  mismatch and no register-order trap. Re-pins `Cirreum.Contracts` `1.1.0` → `1.1.1`.
- Renamed `CacheExpirationSettings` → `CacheExpirationPolicy` and adopted the
  `Cirreum.Caching.Configuration` namespace for `CacheSettings` / `CacheExpirationOverride` (follows
  `Cirreum.Contracts` 1.1.1).

### Fixed

- `InMemoryCacheService` opportunistically evicts expired entries via a single-sweeper pass (triggered on
  cache misses), so a high-cardinality key that expires and is never re-requested no longer lingers
  indefinitely. `RemoveByTagsAsync` now evicts in a single dictionary scan (was one full scan per tag),
  with a null-guard and value-checked removal.

> Breaking, shipped as a pre-adoption patch (1.1.1) via `-AllowBreakingPatch`. Apps that selected a cache
> provider via `Cirreum:Cache:Provider` must now call the matching `Add*CacheService` (in-memory is
> `AddInMemoryCacheService()`).

## [1.1.0] - 2026-06-05

### Changed

- Bumped `Cirreum.Contracts` `1.0.0` → `1.1.0` and `Cirreum.Exceptions` `1.0.4` → `1.1.0`.
  Together these bring `Cirreum.Result` `2.0.0` into Domain's dependency closure,
  which fixes the `Result`/`Result<T>` System.Text.Json round-trip — the
  `QueryCaching` intercept can now cache a `Result` through a serializing cache
  provider without a serialized success deserializing back as a failure. Also
  surfaces `Cirreum.Exceptions` `1.1.0`'s `IErrorState` opt-in, so a
  `NotFoundException` failure carries its keys across the round-trip onto
  `SurrogateResultException.State`. Domain's own public surface is unchanged;
  consumers that use the re-exposed `Cirreum.Result` pagination types should review
  the `Cirreum.Result` 2.0.0 migration notes.

## [1.0.0] - 2026-06-04

### Added

- Initial release. Cirreum.Domain is the default implementation of the Cirreum domain-centric application model, established as part of the **Cirreum 1.0 Foundation Reset** wave.
- Absorbs cross-host concrete implementations from former `Cirreum.Core 5.x`:
  - **Conductor concretes** — `Dispatcher`, `Publisher` + `Publisher.Logger`, `PublisherStrategy`, `ConductorBuilder`, intercepts (`HandlerPerformance`, `QueryCaching`, `Validation`), `Internal/*` pipeline machinery, telemetry, logging, `ConductorOptionsBuilder`
  - **Caching concretes** — `InMemoryCacheService`, `InstrumentedCacheService`, `NoCacheService`, `CacheTelemetry`
  - **State concretes** — `ScopedNotificationState`
  - **Presence concretes** — `UserPresenceBuilder`
  - **RemoteServices concretes** — `RemoteClient`, `RemoteClientLogging`, `RemoteClientTelemetry`, `RemoteConnectionBase`
  - **FileSystem concretes** — `FileSystemUtils`, CSV implementations
  - **Extensions** — `ResultExtensions` (FluentValidation → Result glue), `SystemIOExtensions`, format helpers
  - **Authorization concretes** — `DefaultAuthorizationEvaluator`, `DefaultAuthorizationContextAccessor`, `AuthorizationRoleRegistryBase`, `RoleDefinitionScanner`, operation-grant accessor/factory/evaluator + grant cache machinery, `ResourceAccessEvaluator`, the FluentValidation-based validation subsystem (`IPolicyValidator`, `IAuthorizationConstraint`, `AuthorizerBase`, `AttributeValidatorBase`, `Has*Validator` family), and authorization diagnostics
- The default implementation of the cross-host triad: `Cirreum.Kernel` → `Cirreum.Contracts` → `Cirreum.Domain`.
- References `Cirreum.Contracts` for the abstractions and `Cirreum.Kernel` for foundational types (published packages).

### Migration

Apps consuming concrete impls from `Cirreum.Core 5.x` migrate by installing `Cirreum.Domain` (typically transitive through the runtime package for your host). Namespace `Cirreum.Conductor.*`, `Cirreum.Caching.*`, `Cirreum.State.*`, `Cirreum.Presence.*`, `Cirreum.RemoteServices.*`, `Cirreum.FileSystem.*` preserved.
