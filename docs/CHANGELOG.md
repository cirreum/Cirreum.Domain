# Cirreum.Domain Changelog

All notable changes to **Cirreum.Domain** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

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
