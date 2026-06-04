# Cirreum.Domain

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Domain.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Domain/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Domain.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Domain/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Domain?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Domain/releases)
[![License](https://img.shields.io/github/license/cirreum/Cirreum.Domain?style=flat-square&labelColor=1F1F1F&color=F2F2F2)](https://github.com/cirreum/Cirreum.Domain/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**The default implementation of Cirreum's domain-centric application model.**

## Overview

**Cirreum.Domain** is the default implementation of Cirreum's domain-centric application model — the runtime-agnostic engine that supplies the concrete classes behind the contracts declared in `Cirreum.Contracts`.

Cirreum.Domain provides:

- **Conductor** — the CQRS engine: `Dispatcher`, `Publisher`, `ConductorBuilder`, the pipeline machinery, and the validation / query-caching / handler-performance intercepts
- **Caching** — `InMemoryCacheService`, `InstrumentedCacheService`, `NoCacheService`, and cache telemetry
- **Authorization** — `DefaultAuthorizationEvaluator`, the operation-grant accessor / factory / evaluator and grant-cache machinery, `ResourceAccessEvaluator`, `RoleDefinitionScanner`, and the FluentValidation validators (`AuthorizerBase`, the `Has*Validator` family)
- **State** — `ScopedNotificationState`
- **Presence** — `UserPresenceBuilder`
- **RemoteServices** — `RemoteClient` and the connection base
- **FileSystem** — `FileSystemUtils` and the CSV implementations
- **Registration & extensions** — `AddDomainServices` (the default Conductor pipeline + authorization wiring), `ResultExtensions`, `SystemIOExtensions`, and format helpers

## Where it fits

Cirreum.Domain is **L3 — the framework engine**, implemented over `Cirreum.Contracts` (L2, the contract surface) and `Cirreum.Kernel` (L1, the dependency-free floor), both referenced as published packages. It carries no host or provider dependencies, so the same domain core runs unchanged on a server, in WebAssembly, or in a serverless function.

## Contribution Guidelines

1. **Be conservative with new abstractions**  
   The API surface must remain stable and meaningful — Domain is the engine the whole framework runs on; changes ripple through the entire ecosystem.

2. **Limit dependency expansion**  
   Only add foundational, version-stable dependencies. Concrete dependencies belong here (not in `Cirreum.Contracts`), but keep them deliberate.

3. **Favor additive, non-breaking changes**  
   Breaking changes cascade through every dependent package and every Cirreum app. Major version bumps are rare.

4. **Include thorough unit tests**  
   All behavior should be independently testable.

5. **Document architectural decisions**  
   Context and reasoning should be clear for future maintainers.

6. **Follow .NET conventions**  
   Use established patterns from `Microsoft.Extensions.*` libraries.

## Versioning

Cirreum.Domain follows [Semantic Versioning](https://semver.org/):

- **Major** — Breaking API changes
- **Minor** — New features, backward compatible
- **Patch** — Bug fixes, backward compatible

Given its foundational role, major version bumps are rare and carefully considered.

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*
