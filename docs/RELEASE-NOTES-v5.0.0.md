# Cirreum.Domain 5.0.0 — the connection base follows its contracts

## Why this release exists

`Cirreum.Contracts` 5.0.0 moved the connection contracts into
`Cirreum.RemoteServices.Connections`, on the grounds that a remote service and a remote connection
are two features rather than one: a service is something you *call*, where the exchange is the unit
of work and the caller always initiates; a connection is something you *hold open*, where the channel
is the unit, it has a lifetime the application observes, and either end may speak first.

`RemoteConnectionBase` implements those contracts. Leaving it behind would have meant importing two
namespaces to write a single connection type.

## What changed

`RemoteConnectionBase` moves to `Cirreum.RemoteServices.Connections`. No member of it changed — the
state machine, the atomic transition, the lifecycle hooks, `State` and `StateChanged` are all exactly
as they were in 4.3.1.

`RemoteClient` and the rest of `Cirreum.RemoteServices` stay where they are. This package's
request/response half is untouched.

See [MIGRATION-v5.md](MIGRATION-v5.md).

## Compatibility

- **Applications that do not derive from `RemoteConnectionBase`**: a version bump.
- **Applications that do**: one `using`. Most reach it through a transport base
  (`SignalRRemoteConnection`, `WebSocketRemoteConnection`) rather than directly, and those packages
  carry the move in their own releases.

## See also

- `Cirreum.Contracts` 5.0.0 — the contracts this follows, and the reshaped credential seam that
  prompted the wider release.
