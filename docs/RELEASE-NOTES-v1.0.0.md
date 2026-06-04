# Cirreum.Domain 1.0.0 — The default implementation of the domain model

`Cirreum.Domain` supplies the concrete classes behind `Cirreum.Contracts`'s abstractions: Conductor's dispatcher and publisher, the cache services, the Authorization-pillar evaluators, grant-cache machinery and validators, and the cross-host extensions. It is the runtime-agnostic engine every Cirreum application runs on. 1.0.0 completes the cross-host foundation triad — `Cirreum.Kernel` → `Cirreum.Contracts` → `Cirreum.Domain`.

**Strictly additive — initial release.** No predecessor `Cirreum.Domain` package. Targets .NET 10.0.

---

## Why this release exists

The **Cirreum 1.0 Foundation Reset** keeps cross-host *abstractions* (in `Cirreum.Contracts`) separate from their *implementations* (here). That split is what lets an application depend on the contracts — `IDispatcher`, `ICacheService`, `IAuthorizer` — while the concretes are supplied once, host-agnostically, and swapped or instrumented without touching consumers.

Domain is where those concretes live, free of any host or provider dependency.

---

## What's new

### Conductor concretes

```csharp
// The dispatch pipeline that backs IDispatcher / IPublisher.
services.AddDomainServices(/* applyDefaultPipeline: true */);
```

`Dispatcher`, `Publisher` (+ logging and `PublisherStrategy`), `ConductorBuilder` / `ConductorOptionsBuilder`, the intercepts (`HandlerPerformance`, `QueryCaching`, `Validation`), the internal pipeline machinery, and telemetry — the working CQRS engine behind the contract surface.

### Caching, State, Presence, RemoteServices, FileSystem concretes

`InMemoryCacheService`, `InstrumentedCacheService`, `NoCacheService` and cache telemetry; `ScopedNotificationState`; `UserPresenceBuilder`; `RemoteClient` (+ logging/telemetry) and `RemoteConnectionBase`; `FileSystemUtils` and the CSV implementations — one host-agnostic implementation of each contract.

### Authorization concretes

```csharp
public sealed class OperationGrantCacheInvalidator : IOperationGrantCacheInvalidator {
    // tag-based eviction of cached grants on a grant/revoke.
    public ValueTask InvalidateCallerAsync(string callerId, CancellationToken ct = default);
}
```

`DefaultAuthorizationEvaluator`, `DefaultAuthorizationContextAccessor`, `AuthorizationRoleRegistryBase`, `RoleDefinitionScanner`, the operation-grant accessor/factory/evaluator and grant-cache machinery (including the tag-based cache invalidator), `ResourceAccessEvaluator`, and the FluentValidation-based validation subsystem (`AuthorizerBase`, `IPolicyValidator`, `IAuthorizationConstraint`, the `Has*Validator` family) plus authorization diagnostics. The pillar's *contracts* live in `Cirreum.Contracts`; their *behavior* lives here.

### Registration & extensions

The cross-host registration surface (`AddDomainServices` — the default Conductor pipeline, the FluentValidation/authorization assembly scan, the default authorization evaluator, grant authorization, resource access) and the shared extensions (`ResultExtensions`, `SystemIOExtensions`, format helpers).

---

## Why this lives in Cirreum.Domain

Keeping implementations out of `Cirreum.Contracts` means consumers bind to contracts, not concretes, and the framework can instrument or replace a concrete in one place. Domain references **only** `Cirreum.Contracts` and `Cirreum.Kernel`, and takes no host or provider dependency — it stays a pure, runtime-agnostic implementation layer. The cross-host triad reads `Cirreum.Kernel` → `Cirreum.Contracts` → `Cirreum.Domain`.

---

## Coordinated downstream work

Domain publishes last of the cross-host foundation triad, after Kernel and Contracts. The host-infrastructure and runtime layers compose it into a working application — usually pulling it in transitively.

---

## Compatibility

- **Additive.** Initial release.
- **.NET 10.0.**
- References `Cirreum.Contracts` (abstractions) and `Cirreum.Kernel` (foundational types) as published packages; no host or provider references.
- **Migration from `Cirreum.Core 5.x`:** install `Cirreum.Domain` (typically transitive through the runtime package for your host). Namespaces are preserved — `Cirreum.Conductor.*`, `Cirreum.Caching.*`, `Cirreum.State.*`, `Cirreum.Presence.*`, `Cirreum.RemoteServices.*`, `Cirreum.FileSystem.*`, `Cirreum.Authorization.*`.

---

## See also

- `CHANGELOG.md` — condensed change list for `1.0.0`.
