# Cirreum.Domain v4 → v5 Migration

v5 carries one breaking change: **`RemoteConnectionBase` moves to the
`Cirreum.RemoteServices.Connections` namespace**, following the contracts it implements
(`Cirreum.Contracts` 5.0.0).

No member of it changed. If your application does not derive from `RemoteConnectionBase`, upgrading
is a version bump.

---

## The move

| v4 | v5 |
| --- | --- |
| `Cirreum.RemoteServices.RemoteConnectionBase` | `Cirreum.RemoteServices.Connections.RemoteConnectionBase` |

```csharp
// v4
using Cirreum.RemoteServices;

// v5
using Cirreum.RemoteServices.Connections;
```

Nothing else in this package moved. `RemoteClient`, `RemoteClientLogging`, `RemoteClientTelemetry`
and `ApplicationUserEndpoint` stay in `Cirreum.RemoteServices`.

## Why

`Cirreum.RemoteServices` held two features. A remote service is something you *call* — the exchange
is the unit of work, and the caller always initiates. A remote connection is something you *hold
open* — the channel is the unit, it has a lifetime the application observes, and either end may
speak first.

The second is a relationship with a remote service rather than a peer of one, so it nests. The
inbound side already reads this way: `Cirreum.Invocation.Connections` holds the long-lived sub-types
of `Cirreum.Invocation`.

`Cirreum.Contracts` 5.0.0 moves the connection contracts; this release moves the base class that
implements them, so an application does not import two namespaces to write one connection type.

## Who this reaches

Applications rarely derive from `RemoteConnectionBase` directly — a connection type derives from a
transport base (`SignalRRemoteConnection`, `WebSocketRemoteConnection`), which derives from this one.
Those transport packages carry the move in their own releases, and an application feels it as the
single `using` change above.

## What didn't change

- Every member of `RemoteConnectionBase`: the state machine, `TransitionTo`, the lifecycle hooks,
  `State` and `StateChanged`.
- The rest of `Cirreum.Domain`, which this release does not touch.

## Paired release

| Package | Version |
| --- | --- |
| `Cirreum.Contracts` | 5.0.0 — moves the connection contracts and reshapes the credential seam |
