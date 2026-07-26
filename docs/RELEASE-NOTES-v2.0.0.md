# Cirreum.Domain 2.0.0 — Domain Events, and Four Metrics You Should Re-point

## Why this release exists

`Cirreum.Domain` implements the Conductor pipeline, so it follows `Cirreum.Kernel` 2.0.0's rename of
the publish/subscribe markers. That part is mechanical.

The part to read is the **metric renames**, because they are the only change in this wave that
breaks something outside your source tree.

## Read this first if you have dashboards

Four Conductor instruments are renamed. Nothing else in this release affects a running system, but
these will silently empty any chart, alert, or saved query bound to the old names:

| Before | After |
|---|---|
| `conductor.notifications.total` | `conductor.domain_events.total` |
| `conductor.notifications.failed.total` | `conductor.domain_events.failed` |
| `conductor.notifications.no_handlers.total` | `conductor.domain_events.no_handlers` |
| `conductor.notifications.duration` | `conductor.domain_events.duration` |

Note the third column of that change that isn't visible above: **`.failed.total` became `.failed`.**
The operation instruments have always been `conductor.operations.failed` and
`conductor.operations.canceled`, so the domain-event pair were the outliers carrying a redundant
suffix. Fixing the name was the moment to fix the shape.

All four are now `public const` on `ConductorTelemetry`, beside the operation metrics, rather than
inline string literals. The next rename will be a compile-time reference rather than a search.

## Domain events

`INotification` → `IDomainEvent`, `INotificationHandler<T>` → `IDomainEventHandler<T>`.

Cirreum had used "notification" for two opposite concepts: Conductor's in-application
publish/subscribe, and the human-facing state family a client binds to in order to show a person
something. "Notification handler" meant *reacts to something that happened* or *renders something
for a user* depending on which package you were reading.

For a consuming application this is one line per handler:

```csharp
// Before
public sealed class OrderPlacedHandler : INotificationHandler<OrderPlaced> {
	public Task HandleAsync(OrderPlaced notification, CancellationToken cancellationToken) { }
}

// After
public sealed class OrderPlacedHandler : IDomainEventHandler<OrderPlaced> {
	public Task HandleAsync(OrderPlaced domainEvent, CancellationToken cancellationToken) { }
}
```

Registration, discovery, dispatch, fan-out, and publishing strategies are all unchanged.

**`ScopedNotificationState` keeps its name**, as do `INotificationState` and
`IScopedNotificationState` in `Cirreum.Contracts`. They are the human-facing concept, and separating
the two is the entire point — a project-wide find/replace of "Notification" undoes this release.

## Also here: a profile-enrichment fix

`ClaimsUserProfileEnricher` now resolves `DisplayName`'s name rung through the identity's configured
name claim type rather than the literal `"name"` claim.

A provisioned name is canonicalized client-side onto `ClaimsIdentity.NameClaimType`, so an
application that configured anything other than `"name"` had that name skipped entirely —
`DisplayName` fell through to the given-plus-family composite, or to `null`. Applications on the
default type are unaffected.

## Compatibility

Breaking. Source changes are compile errors; the metric renames are the only silent break, and they
affect dashboards rather than code.

See [`MIGRATION-v2.md`](MIGRATION-v2.md) for the find/replace tables and the behavior notes.

## Coordinated downstream work

Part of the `Cirreum.Kernel` 2.0.0 wave, alongside `Cirreum.Contracts`,
`Cirreum.Messaging.Distributed`, `Cirreum.AuthenticationProvider`,
`Cirreum.Runtime.AuthenticationProvider`, and `Cirreum.Runtime.Messaging`, with
`Cirreum.Authentication.External` and `Cirreum.Services.Wasm` taking patches.

`Cirreum.Runtime.AuthenticationProvider` renames an instrument in the same wave for the same reason
— `auth_transformations_total` → `cirreum.authn.transformations` — so an observability pass covering
both is worth doing once.

## See also

- [`MIGRATION-v2.md`](MIGRATION-v2.md)
- [`CHANGELOG.md`](CHANGELOG.md)
- `Cirreum.Kernel` 2.0.0 release notes — the origin of the rename
