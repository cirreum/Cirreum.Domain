# Cirreum.Domain Changelog

All notable changes to **Cirreum.Domain** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Updated

- Updated NuGet packages.

## [1.3.0] - 2026-07-24

### Added

- `ClaimsUserProfileEnricher` now consolidates `UserProfile.DisplayName` from claims alone:
  `Nickname` (from the `nickname` claim), then the `name` claim, then a `GivenName` +
  `FamilyName` composite â€” whichever is available first. `Nickname` goes first because
  `UserProfile.Name` is already resolved from the `name` claim at construction, so trying
  `name` first for `DisplayName` would just duplicate `Name`. The step is fill-only (`??=`),
  so a richer, provider-specific enrichment that runs before or after it (e.g. Microsoft Graph
  in `Cirreum.Runtime.Wasm.Msal`) may still take the slot with its own value; correctness does
  not depend on enrichment order. Previously `DisplayName` was only ever set by Graph
  enrichment, so any client without it (every `Cirreum.Runtime.Wasm.Oidc` app) saw a
  permanently-`null` `DisplayName` regardless of what the token carried.

## [1.2.7] - 2026-07-20

### Updated

- Updated NuGet packages.

## [1.2.6] - 2026-07-19

### Updated

- Updated NuGet packages.

## [1.2.5] - 2026-07-07

### Updated

- Updated NuGet packages. *(Entry backfilled â€” the 1.2.2â€“1.2.5 dependency-bump releases shipped
  without changelog entries.)*

## [1.2.4] - 2026-07-05

### Updated

- Updated NuGet packages. *(Entry backfilled.)*

## [1.2.3] - 2026-07-05

### Updated

- Updated NuGet packages. *(Entry backfilled.)*

## [1.2.2] - 2026-07-05

### Updated

- Updated NuGet packages. *(Entry backfilled.)*

## [1.2.1] - 2026-07-05

### Added

- `GrantsInvalidatedCacheHandler` â€” the framework-shipped consumer of Kernel's `GrantsInvalidated` auth event, registered automatically wherever grant authorization is registered. Calls `IOperationGrantCacheInvalidator.InvalidateCallerAsync` for the subject only â€” `InvalidateFeatureAsync` is a different, broader operation (evicts across every caller for a feature, not just this subject) and is never invoked from this handler. Part of ADR-0027's auth-event delivery wave; won't receive events until `Cirreum.Runtime.Authentication`'s in-process publisher (ADR-0025) ships. *(Entry backfilled â€” this shipped in 1.2.1 but was left under Unreleased at release time.)*

### Updated

- Re-pinned `Cirreum.Contracts` `1.2.0` â†’ `1.2.1`.

## [1.2.0] - 2026-07-04

### Added

- **`ClaimsUserProfileEnricher`** â€” the default claims-based `IUserProfileEnricher` implementation, relocated here from `Cirreum.AuthenticationProvider`, alongside its interface counterpart `IUserProfileEnrichmentBuilder` (now in `Cirreum.Contracts 1.2.0`). Same reasoning as `UserPresenceBuilder`'s existing placement here: host-agnostic profile enrichment belongs in the spine, not the Authentication feature track.

### Changed

- Re-pinned `Cirreum.Contracts` `1.1.1` â†’ `1.2.0`.

## [1.1.2] - 2026-07-04

### Fixed

- **`IUserPresenceBuilder.AddPresenceService<T>(refreshInterval)` actually ships now.** This convenience registration (browser-only; registers the presence service scoped + configures `UserPresenceMonitorOptions.RefreshInterval` via `PostConfigure`) lived in legacy `Cirreum.Core 5.x` but was never ported when `IUserPresenceBuilder`/`UserPresenceBuilder` moved to Contracts/Domain during the foundation reset â€” silently blocking `Cirreum.Graph.Provider`'s cutover off `Cirreum.Core`. Ported verbatim as an extension method alongside `UserPresenceBuilder` (same namespace, same behavior).

## [1.1.1] - 2026-07-03

### Changed

- **Code-first cache provider selection.** `AddCirreumCaching` now registers the settings + a no-op
  default only; choose a provider explicitly via `AddInMemoryCacheService()` (or an infrastructure
  package's `Add*CacheService`). A new public `AddCacheService(factory)` helper lets provider packages
  set the active `ICacheService` (with telemetry + keyed consumers), *replacing* any prior registration,
  so it works in any order after `AddCirreumCaching` / `AddDomainServices`. Also removes the internal
  `QueryCachingDiagnostics` misconfiguration warning â€” obsolete now that there is no provider enum to
  mismatch and no register-order trap. Re-pins `Cirreum.Contracts` `1.1.0` â†’ `1.1.1`.
- Renamed `CacheExpirationSettings` â†’ `CacheExpirationPolicy` and adopted the
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

- Bumped `Cirreum.Contracts` `1.0.0` â†’ `1.1.0` and `Cirreum.Exceptions` `1.0.4` â†’ `1.1.0`.
  Together these bring `Cirreum.Result` `2.0.0` into Domain's dependency closure,
  which fixes the `Result`/`Result<T>` System.Text.Json round-trip â€” the
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
  - **Conductor concretes** â€” `Dispatcher`, `Publisher` + `Publisher.Logger`, `PublisherStrategy`, `ConductorBuilder`, intercepts (`HandlerPerformance`, `QueryCaching`, `Validation`), `Internal/*` pipeline machinery, telemetry, logging, `ConductorOptionsBuilder`
  - **Caching concretes** â€” `InMemoryCacheService`, `InstrumentedCacheService`, `NoCacheService`, `CacheTelemetry`
  - **State concretes** â€” `ScopedNotificationState`
  - **Presence concretes** â€” `UserPresenceBuilder`
  - **RemoteServices concretes** â€” `RemoteClient`, `RemoteClientLogging`, `RemoteClientTelemetry`, `RemoteConnectionBase`
  - **FileSystem concretes** â€” `FileSystemUtils`, CSV implementations
  - **Extensions** â€” `ResultExtensions` (FluentValidation â†’ Result glue), `SystemIOExtensions`, format helpers
  - **Authorization concretes** â€” `DefaultAuthorizationEvaluator`, `DefaultAuthorizationContextAccessor`, `AuthorizationRoleRegistryBase`, `RoleDefinitionScanner`, operation-grant accessor/factory/evaluator + grant cache machinery, `ResourceAccessEvaluator`, the FluentValidation-based validation subsystem (`IPolicyValidator`, `IAuthorizationConstraint`, `AuthorizerBase`, `AttributeValidatorBase`, `Has*Validator` family), and authorization diagnostics
- The default implementation of the cross-host triad: `Cirreum.Kernel` â†’ `Cirreum.Contracts` â†’ `Cirreum.Domain`.
- References `Cirreum.Contracts` for the abstractions and `Cirreum.Kernel` for foundational types (published packages).

### Migration

Apps consuming concrete impls from `Cirreum.Core 5.x` migrate by installing `Cirreum.Domain` (typically transitive through the runtime package for your host). Namespace `Cirreum.Conductor.*`, `Cirreum.Caching.*`, `Cirreum.State.*`, `Cirreum.Presence.*`, `Cirreum.RemoteServices.*`, `Cirreum.FileSystem.*` preserved.
