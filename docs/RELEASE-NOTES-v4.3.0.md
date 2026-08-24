# Cirreum.Domain 4.3.0 — the remote-connection base gains lifecycle hooks and an atomic state machine

## Why this release exists

`RemoteConnectionBase` has described the caller-side connection state machine since the
foundation reset, but nothing derived from it. Transport implementations are now being built
against it, and building them surfaced two gaps that only appear once a real transport drives
the base.

## Lifecycle hooks

A transport reconnect restores the *channel*. It does not restore what the application had
established over that channel — group membership, presence announcements, subscriptions the
server tracks per connection. Without a defined place for that work, every transport
implementation invents one, and the failure mode is quiet: the reconnect succeeds, the
connection reports `Connected`, and messages the caller expects simply never arrive.

Two protected virtuals now mark the moment:

```csharp
protected virtual Task OnConnectedAsync(CancellationToken cancellationToken);
protected virtual Task OnReconnectedAsync(CancellationToken cancellationToken);
```

Both default to no-ops, and both run after the transport connects and **before** the
connection reports `Connected` — so their effects are complete when a listener observes the
state. An exception from either fails the attempt, leaving a transport's retry policy to
continue in the reconnect case.

## Atomic state transitions

`TransitionTo` performed a read-modify-write on an unsynchronized field:

```csharp
var previous = this._state;
if (previous == newState) { return; }
this._state = newState;
```

That was adequate while nothing implemented the base. It is not adequate for a real transport:
state changes arrive from transport callbacks and from background reconnect loops while a
caller may be inside `ConnectAsync` or `DisconnectAsync`. Two concurrent transitions could
both observe the same prior state and each raise `StateChanged` for what was one logical
change — a UI bound to the event sees a transition that did not happen twice.

`State` now reads through `Volatile.Read`, and the swap runs through `Interlocked.Exchange`:

| Guarantee | Meaning |
| --------- | ------- |
| Every event reports a real transition | `PreviousState` is the state the connection actually moved from |
| One logical transition, one event | Concurrent callers targeting the same state raise once |
| `State` is the authority | Concurrent transitions may raise their events in an order other than the one they were applied in |

The third row is stated rather than fixed. Ordering events under a lock would mean invoking
listeners while holding it, which trades a benign reordering for a deadlock risk.

## Compatibility

* **Additive and behavioral.** No member signatures changed.
* The hooks are new virtuals with default implementations — existing derived types (there are
  none in the framework) compile unchanged.
* The state machine's observable contract only narrows: events that would previously have been
  raised twice for one change are now raised once.

## See also

* `IRemoteConnection`, `RemoteConnectionState` — the contract in `Cirreum.Contracts`.
* `RemoteClient` — the request/response sibling in the same family.
