# Cirreum.Domain 1.1.1

## Summary

Adopts the **code-first caching foundation** from `Cirreum.Contracts 1.1.1`: a cache
provider is chosen by the registration call rather than an appsettings switch. Renames a
cache type, moves the app-author configuration types into `Cirreum.Caching.Configuration`,
and tightens in-memory eviction.

These are **breaking** changes, shipped as a **pre-adoption patch** (`1.1.0 → 1.1.1`, via
`-AllowBreakingPatch`) — the caching surface has essentially no consumers yet, so this is
part of finalizing the caching foundation bottom-up alongside `Cirreum.Contracts 1.1.1`.

## Why

`Cirreum.Domain` hosts the caching registration and the `InMemoryCacheService`. With the
`CacheProvider` enum removed upstream, provider selection becomes the `Add…CacheService`
call, and Domain provides the seam (`AddCacheService(factory)`) that provider packages use
to install the active `ICacheService` in any registration order.

## What changed

### Changed

- **Code-first cache provider selection.** `AddCirreumCaching` now registers the settings +
  a no-op default only; choose a provider explicitly via `AddInMemoryCacheService()` (or an
  infrastructure package's `Add*CacheService`). A new public **`AddCacheService(factory)`**
  helper lets provider packages set the active `ICacheService` (with telemetry + keyed
  consumers), *replacing* any prior registration, so it works in any order after
  `AddCirreumCaching` / `AddDomainServices`.
- Removed the internal `QueryCachingDiagnostics` misconfiguration warning — obsolete now
  that there is no provider enum to mismatch and no register-order trap.
- **Renamed `CacheExpirationSettings` → `CacheExpirationPolicy`** and adopted the
  **`Cirreum.Caching.Configuration`** namespace for `CacheSettings` / `CacheExpirationOverride`
  (follows `Cirreum.Contracts 1.1.1`).
- Re-pins `Cirreum.Contracts 1.1.0 → 1.1.1`.

### Fixed

- `InMemoryCacheService` opportunistically evicts expired entries via a single-sweeper pass
  (triggered on cache misses), so a high-cardinality key that expires and is never re-requested
  no longer lingers indefinitely. `RemoveByTagsAsync` now evicts in a single dictionary scan
  (was one full scan per tag), with a null-guard and value-checked removal.

## Migration

1. Replace any `Cirreum:Cache:Provider` appsettings selection with the matching registration
   call — in-memory is `AddInMemoryCacheService()`.
2. `CacheExpirationSettings` → `CacheExpirationPolicy`.
3. Add `using Cirreum.Caching.Configuration;` where you configure `CacheSettings` /
   `CacheExpirationOverride`.

## Compatibility

- **Breaking, shipped as a patch** (`-AllowBreakingPatch`) — justified by essentially zero
  consumers of the caching surface pre-adoption.
- **Depends on `Cirreum.Contracts 1.1.1`**, `Cirreum.Exceptions 1.1.0`.
