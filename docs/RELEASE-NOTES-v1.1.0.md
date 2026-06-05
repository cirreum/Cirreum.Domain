# Cirreum.Domain 1.1.0

## Summary

A minor release that advances Domain's foundation dependencies so the
`Cirreum.Result` serialization fix reaches the query-caching pipeline. Domain's
own public surface is unchanged.

## Why

`Cirreum.Domain` hosts the `QueryCaching` Conductor intercept, which caches the
whole `Result<TValue>` returned by a handler through `ICacheService`. Before
`Cirreum.Result` 2.0.0, `Result`/`Result<T>` could not round-trip through
System.Text.Json — a serialized success deserialized as a failure — so any
**serializing** cache provider (distributed/hybrid) corrupted cached successes.
Domain shipped 1.0.0 against the pre-fix `Result` (via `Contracts` 1.0.0), so this
release re-pins the foundation to propagate the fix.

## What changed

- **`Cirreum.Contracts` `1.0.0` → `1.1.0`** — brings `Cirreum.Result` `2.0.0` into
  Domain's dependency closure: the canonical `Result`/`Result<T>` System.Text.Json
  converter, the `SurrogateResultException` carrier + `HasError` matchers, and the
  rewritten pagination types.
- **`Cirreum.Exceptions` `1.0.4` → `1.1.0`** — surfaces the `IErrorState` opt-in, so
  a `NotFoundException` failure carries its keys across a serializing round-trip
  onto `SurrogateResultException.State["keys"]` instead of losing them.

## Effect on query caching

With this release, the `QueryCaching` intercept caches `Result<T>` correctly under
every provider. A cached success comes back a success; a cached `NotFoundException`
failure comes back as a `SurrogateResultException` whose type name and keys are
preserved (consumers branch with `HasError<NotFoundException>()` rather than
`Error is NotFoundException`).

## Compatibility

- **Minor release.** Domain's own public API is unchanged.
- Domain transitively re-exposes the `Cirreum.Result` pagination types
  (`SliceResult`/`CursorResult`/`PagedResult`), which were source-rewritten in
  `Result` 2.0.0 (positional records → explicit-ctor: `Deconstruct`/`with` removed,
  ctor params renamed). Consumers that deconstruct or `with`-copy those types via
  Domain must follow the `Cirreum.Result` 2.0.0 migration guide. Consumers that
  don't use pagination are unaffected.

## Upgrade

Update the package reference to `1.1.0`. No Domain-specific code changes required;
review `Cirreum.Result` 2.0.0's migration notes only if you use the pagination types.
