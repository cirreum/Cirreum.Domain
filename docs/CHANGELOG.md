# Cirreum.Domain Changelog

All notable changes to **Cirreum.Domain** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

## [4.2.0] - 2026-08-04

### Added

- **`ApplicationUserEndpoint.Route`** (`/_cirreum/application-user`) — the framework-owned
  application-user bootstrap route, shared by the server host (which maps it) and the
  WebAssembly client (which calls it) so the two ends cannot drift. The `/_cirreum/` prefix
  is the framework's reserved route namespace. Server mapping and client registration ship
  in the paired `Cirreum.Runtime.Server` / `Cirreum.Runtime.Wasm` releases.

### Updated

- Re-pinned `Cirreum.Contracts` `4.1.0` → `4.2.0` (removes the dormant, never-referenced
  `AuthorizationDenial` record; `DenyCodes` documentation now states codes reach telemetry only).

### Fixed

- **401/403 responses no longer surface raw JSON as the exception message.** `RemoteClient`
  read the denial body as a plain string, so in production `ForbiddenAccessException.Message`
  was a problem-details blob (`{"type":...,"title":"Forbidden",...}`) — which apps surfacing
  `ex.Message` rendered to users. Denial bodies carrying JSON are now parsed and the safe
  `Detail` used; non-JSON bodies (typically empty, from authentication middleware) fall back
  to the raw string, then the reason phrase.

## [4.1.0] - 2026-08-03

### Fixed

- **The disabled-user check no longer reads an absent application user as a disabled one.**
  Callers with no application-user record — operator and machine tracks, where disablement is
  enforced at the identity provider — were denied `USER_DISABLED` on every grantable
  operation, before `ShouldBypassAsync` could grant them anything. The check now denies only
  on an explicit `IsEnabled = false` and ignores application-user load state entirely.
- **Disabled users whose type does not implement `IOwnedApplicationUser` are now denied.** The
  check pattern-matched the owned interface before reading `IsEnabled`, which is declared on
  `IApplicationUser`, so apps with no owner dimension on their user type were never covered.

### Changed

- **The disabled-user check moved from `OperationGrantEvaluator` to
  `DefaultAuthorizationEvaluator`**, where it runs immediately after the authentication check
  for every authorizable object. It previously ran only for operations implementing a
  `IGrantable*Base` interface, so a disabled user retained full access to every non-grantable
  operation. ⚠️ **Disabled callers who reach non-grantable operations today will be denied
  after upgrade.** Apps that disable a user by stripping their roles will also see the denial
  reason change from `has no assigned roles` to `User is disabled.` — the disabled gate now
  runs ahead of the no-roles gate so the specific fact wins.
- `USER_DISABLED` remains the deny code and the span reason, and the denial remains a 403
  `ForbiddenAccessException`.
- **The four preflight gates now record decisions.** Authentication, application-user,
  roles, and authorizer-presence denials report `stage = preflight` with their own step and a
  `DenyCodes` reason, so they appear on `cirreum.authz.decisions` for the first time. ⚠️ Their
  reason values change with them: `"unauthenticated"` → `AUTHENTICATION_REQUIRED`,
  `"no-roles"` → `NO_ROLES_ASSIGNED`, `"no-authorizers"` → `NO_AUTHORIZERS_REGISTERED`,
  `"error"` → `EVALUATION_ERROR`. **Queries matching the old lowercase values will go quiet.**
- **Constraint, object-authorizer, and policy denials now set a reason on the span.** They
  recorded a reason to the decisions counter but passed only `denyStage` to the duration call,
  and `RecordDecision` tags no activity — so those spans carried `decision=deny` with no
  reason at all. Grant denials gain the same, having passed no reason either.
- Denials with no `ErrorCode` report `DenyCodes.Unknown` rather than a `"UNKNOWN"` literal.

### Added

- **Six tests covering the disabled-user gate**, which previously had none: loaded-with-null
  passes through to grant resolution, enabled and disabled owned users, an enabled and a
  disabled user whose type is not owned, enforcement on a non-grantable operation, and the
  disabled reason winning over the no-roles reason.

### Updated

- Re-pinned `Cirreum.Contracts` `4.0.1` → `4.1.0`, which carries the `preflight` stage, its four
  steps, and the deny codes this release reports against.

## [4.0.1] - 2026-07-31

### Updated

- Re-pinned `Cirreum.Contracts` `4.0.0` → `4.0.1` (carries `Cirreum.Kernel` 2.0.1, the
  documentation-only patch completing the records-only grant semantics wave bottom-up).

## [4.0.0] - 2026-07-31

### Breaking

- **The grant-factory orchestrator no longer merges an implicit home owner into the granted
  set.** Cold-path resolution is now `ResolveGrantsAsync` alone: an empty granted owner set is
  `Denied`, with no identity-derived fallback. Home-company membership access is expressed as a
  grant record (e.g., a company-self-grant row) — permission-scoped, revocable, and auditable
  like every other grant. ⚠️ **Apps must seed home grant rows BEFORE upgrading or tenant users
  fail closed** — see `MIGRATION-v4.md` and the paired `Cirreum.Contracts` 4.0.0 release, which
  removes `IOperationGrantProvider.ResolveHomeOwnerAsync`.

### Added

- **Dispatch-level regression test for records-only home semantics**: a caller whose
  `ApplicationUser` carries a home `OwnerId` but holds zero grant records is denied, and no
  owner is stamped. Fails loud if an implicit home-owner merge ever returns.

### Updated

- Re-pinned `Cirreum.Contracts` `3.0.0` → `4.0.0`.

## [3.0.0] - 2026-07-30

### Breaking

- **`IPolicyValidator` → `IPolicyAuthorizer`, and its `ValidateAsync` → `EvaluateAsync`.** The
  Stage 3 extension point performs authorization — it runs inside the authorization evaluator,
  denies with `ForbiddenAccessException`, and participates in deny telemetry — while "validator"
  in Cirreum means FluentValidation property validation (the Conductor `Validation` intercept's
  stage). The framework's own registration code already called these "policy authorizers"; the
  public surface now agrees. `PolicyName`, `Order`, `SupportedRuntimeTypes`, `AppliesTo`, and all
  evaluation semantics are unchanged. See `MIGRATION-v3.md`.
- **`AttributeValidatorBase<TAttribute>` → `AttributePolicyAuthorizerBase<TAttribute>`** — the
  attribute-gated policy-authorizer base was never a validator. The file also leaves the
  `Authorization\Validators` folder (home of the genuine property validators); its namespace was
  already `Cirreum.Authorization` and does not change.
- Stage 3 deny telemetry emits step `policy-authorizer` (was `policy-validator`) via the paired
  `Cirreum.Contracts` 3.0.0 rename.

### Updated

- Re-pinned `Cirreum.Contracts` `2.0.1` → `3.0.0` (the paired `StepPolicyAuthorizer` telemetry
  rename).

### Fixed

- **`ResourceAccessEvaluator.CheckAsync(resourceId, …)` resolves the caller before any provider
  I/O, and the not-found path is a pure `Result` path.** Previously the resource-not-found branch
  called `ResolveCaller()` after the provider fetch, binding a `userState` it never consumed — the
  call's only effect was an invariant throw placed where the block's job is to return a failed
  `Result`. The caller resolution is now hoisted to the top of the overload (contextless misuse
  fails fast before touching the provider, consistent with the other entry points), and the
  not-found denial now writes the same `LogResourceAccessDenied` log line as every other deny path
  (previously telemetry-only).

## [2.0.1] - 2026-07-30

### Security

- **Operation-level authorization is enforced again — the `Authorization<,>` intercept is
  registered in the Conductor default pipeline.** The 2.0.0 spine shipped without it:
  `ConductorOptionsBuilder.ConfigureIntercepts` registered only `Validation`,
  `HandlerPerformance`, and `QueryCaching`, deferring the authorization intercepts to a "runtime
  composition layer" that was never built (a comment left over from the pre-reset plan in which
  the intercept belonged to the since-dissolved Authorization track). The effect was fail-open:
  every `IAuthorizableOperationBase` dispatch proceeded straight to its handler — authorizer
  role/claim rules, the Stage 1 grant gate (including `OwnerId` auto-stamping), authorization
  constraints, and policy validators were all silently skipped, with no error, log, or telemetry.
  The default pipeline is now `Validation → Authorization → GrantedLookupAudit → [custom] →
  HandlerPerformance → QueryCaching`, restoring the pre-reset composition and order.
  `Authorization<,>` moved in from `Cirreum.Contracts` to live beside the evaluator it invokes
  and the builder that registers it.
- **`GrantedLookupAudit<,>` (the Pattern C post-fetch ownership audit) is registered as well** —
  it was orphaned by the same omission, so null-`OwnerId` lookup auditing never ran either.
- **Boot-time hard-fail guards against recurrence.** `AddDomainServices` now throws at
  composition time if the default pipeline is ever missing the `Authorization<,>` intercept, and
  reports authorizable operations that no authorizer, grant surface, constraint, or policy
  validator could ever pass (always-deny dead operations) through the deferred startup log —
  which the server runtime converts into a boot failure.

### Fixed

- `AddDomainServices` registers the default `IAuthorizationEvaluator` itself (`TryAdd`,
  idempotent with the host runtimes' existing calls), so the opinionated composition path is
  self-contained instead of relying on every host to call `AddDefaultAuthorizationEvaluator`
  first.

### Updated

- Re-pinned `Cirreum.Contracts` `2.0.0` → `2.0.1`, which removes the orphaned internal intercept
  this release re-homes and documents the home-owner merge semantics.
- Added `Cirreum.Logging.Deferred` `1.0.116` (deferred startup-log reporting for the new
  boot-time validation).

## [2.0.0] - 2026-07-26

### Updated

- Re-pinned `Cirreum.Contracts` → `2.0.0` and `Cirreum.Kernel` → `2.0.0`, which carry the marker rename this release follows.

### Changed

- **Conductor domain-event metrics renamed**, and an inconsistency with the operation instruments
  corrected at the same time:

  | Before | After |
  |---|---|
  | `conductor.notifications.total` | `conductor.domain_events.total` |
  | `conductor.notifications.failed.total` | `conductor.domain_events.failed` |
  | `conductor.notifications.no_handlers.total` | `conductor.domain_events.no_handlers` |
  | `conductor.notifications.duration` | `conductor.domain_events.duration` |

  Note `.failed.total` → `.failed`: the operation instruments have always been
  `conductor.operations.failed` / `.canceled`, so the domain-event pair were the outliers. All four
  are now constants on `ConductorTelemetry` beside the operation metrics rather than inline
  literals, so the next rename is a compile-time reference instead of a search. Dashboards, alerts,
  and saved queries bound to the old names need updating.
- **Conductor's publish/subscribe markers are renamed** — `INotification` → `IDomainEvent`,
  `INotificationHandler<T>` → `IDomainEventHandler<T>` — following `Cirreum.Kernel` 2.0.0.
  Cirreum used "notification" for two unrelated concepts: in-application publish/subscribe, and
  the human-facing state family a client binds to in order to show a person something.
  `IDomainEvent` names the first for what it is; "notification" now refers only to the second.

  **`INotificationState` and `IScopedNotificationState` keep their names** — they are the
  human-facing concept, and preserving that separation is the point of the rename. A project-wide
  find/replace of "Notification" will destroy it.

### Fixed

- `ClaimsUserProfileEnricher` now resolves the `DisplayName` name rung through the identity's
  configured name claim type instead of the literal `"name"` claim. A provisioned `customName`
  is canonicalized client-side onto `ClaimsIdentity.NameClaimType`, so an application that
  configured a name claim type other than `"name"` had its provisioned name skipped, and
  `DisplayName` fell through to the `GivenName` + `FamilyName` composite (or to `null`). Apps on
  the default `"name"` claim type are unaffected. Whitespace-only name claims still fall through
  to the composite, as before.

## [1.3.1] - 2026-07-24

### Updated

- Updated NuGet packages.

## [1.3.0] - 2026-07-24

### Added

- `ClaimsUserProfileEnricher` now consolidates `UserProfile.DisplayName` from claims alone:
  `Nickname` (from the `nickname` claim), then the `name` claim, then a `GivenName` +
  `FamilyName` composite — whichever is available first. `Nickname` goes first because
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

- Updated NuGet packages. *(Entry backfilled — the 1.2.2–1.2.5 dependency-bump releases shipped
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

- `GrantsInvalidatedCacheHandler` — the framework-shipped consumer of Kernel's `GrantsInvalidated` auth event, registered automatically wherever grant authorization is registered. Calls `IOperationGrantCacheInvalidator.InvalidateCallerAsync` for the subject only — `InvalidateFeatureAsync` is a different, broader operation (evicts across every caller for a feature, not just this subject) and is never invoked from this handler. Part of ADR-0027's auth-event delivery wave; won't receive events until `Cirreum.Runtime.Authentication`'s in-process publisher (ADR-0025) ships. *(Entry backfilled — this shipped in 1.2.1 but was left under Unreleased at release time.)*

### Updated

- Re-pinned `Cirreum.Contracts` `1.2.0` → `1.2.1`.

## [1.2.0] - 2026-07-04

### Added

- **`ClaimsUserProfileEnricher`** — the default claims-based `IUserProfileEnricher` implementation, relocated here from `Cirreum.AuthenticationProvider`, alongside its interface counterpart `IUserProfileEnrichmentBuilder` (now in `Cirreum.Contracts 1.2.0`). Same reasoning as `UserPresenceBuilder`'s existing placement here: host-agnostic profile enrichment belongs in the spine, not the Authentication feature track.

### Changed

- Re-pinned `Cirreum.Contracts` `1.1.1` → `1.2.0`.

## [1.1.2] - 2026-07-04

### Fixed

- **`IUserPresenceBuilder.AddPresenceService<T>(refreshInterval)` actually ships now.** This convenience registration (browser-only; registers the presence service scoped + configures `UserPresenceMonitorOptions.RefreshInterval` via `PostConfigure`) lived in legacy `Cirreum.Core 5.x` but was never ported when `IUserPresenceBuilder`/`UserPresenceBuilder` moved to Contracts/Domain during the foundation reset — silently blocking `Cirreum.Graph.Provider`'s cutover off `Cirreum.Core`. Ported verbatim as an extension method alongside `UserPresenceBuilder` (same namespace, same behavior).

## [1.1.1] - 2026-07-03

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
