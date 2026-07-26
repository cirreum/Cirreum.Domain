# Cirreum.Domain v1 → v2 Migration

## Why v2

`Cirreum.Kernel` 2.0.0 renames Conductor's publish/subscribe markers — `INotification` →
`IDomainEvent`, `INotificationHandler<T>` → `IDomainEventHandler<T>` — because Cirreum used
"notification" for two unrelated concepts: in-application publish/subscribe, and the human-facing
state family a client binds to in order to show a person something. `Cirreum.Domain` implements the
Conductor pipeline, so it follows.

The domain-event metric names are corrected in the same release.

## Breaking Changes — Find/Replace Table

| Before | After |
|---|---|
| `INotification` | `IDomainEvent` |
| `INotificationHandler<TNotification>` | `IDomainEventHandler<TDomainEvent>` |
| `HandleAsync(notification, …)` | `HandleAsync(domainEvent, …)` |
| `IPublisher.PublishAsync<TNotification>` | `IPublisher.PublishAsync<TDomainEvent>` |

**Do not rename the notification state family.** `ScopedNotificationState` (in this package),
`INotificationState`, and `IScopedNotificationState` are the human-facing concept and keep their
names. Preserving that separation is the entire point of the change — a project-wide find/replace
of "Notification" will destroy it.

## Migration Walkthrough

### 1. Handlers

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

Registration is unchanged — handlers are still discovered by assembly scan and registered
transient. Dispatch, fan-out, and publishing strategies are unchanged.

### 2. Event types

```csharp
// Before
public sealed record OrderPlaced(Guid OrderId) : INotification;

// After
public sealed record OrderPlaced(Guid OrderId) : IDomainEvent;
```

### 3. Publishing

`IPublisher.PublishAsync` is unchanged at the call site — only its constraint and type-parameter
name moved. Existing `await publisher.PublishAsync(new OrderPlaced(id))` calls compile as-is once
the event type implements `IDomainEvent`.

## Metric Renames

Conductor's domain-event instruments are renamed, and an inconsistency with the operation
instruments is corrected at the same time. Update any dashboards, alerts, or saved queries.

| Before | After |
|---|---|
| `conductor.notifications.total` | `conductor.domain_events.total` |
| `conductor.notifications.failed.total` | `conductor.domain_events.failed` |
| `conductor.notifications.no_handlers.total` | `conductor.domain_events.no_handlers` |
| `conductor.notifications.duration` | `conductor.domain_events.duration` |

Note the `.failed.total` → `.failed` change: the operation instruments have always been
`conductor.operations.failed` and `conductor.operations.canceled`, so the domain-event ones were
the odd pair. All four are now constants on `ConductorTelemetry`, alongside the operation
metrics, rather than inline string literals — so the next rename is a compile-time reference
rather than a search.

Underscores separate words *within* a segment (`domain_events`, `no_handlers`); periods separate
segments. That matches `cirreum.authz.resource_type` and the OpenTelemetry conventions.

## What Didn't Change

- Operation dispatch, validation, authorization, and the `Result` pipeline
- Intercepts (`QueryCaching`, `Validation`, `HandlerPerformance`) and their registration
- Publishing strategies and `PublishingStrategyAttribute` semantics
- `conductor.operations.*` instrument names
- Profile enrichment, caching, presence, remote services, and state

## Downstream Package Impact

`Cirreum.Domain` is Core-layer. Packages above it that define or handle domain events need the
same find/replace; those that only dispatch operations need a re-pin.
