# Backlog

Deferred work for **Cirreum.Domain**. Items here are tracked but not yet ready
to ship — either because the cost outweighs the benefit in isolation, or
because they're waiting on a forcing function (a related change, a consumer
upgrade, a coordinated multi-repo rollout).

## How this file works

- Each item is a `###` heading so it can be linked to and parsed.
- Each item declares **`SemVer:`** (`Patch` | `Minor` | `Major` | `Unspecified`),
  **`Trigger:`** (the human-readable condition that will make it ready), and
  **`Noted:`** (the date the item was added).
- The Cirreum DevOps release scripts (`PatchRelease`, `MinorRelease`,
  `MajorRelease`) surface items at-or-below the requested bump level so the
  operator can decide whether to fold them in before tagging.
- Items that ship: move from this file to `docs/CHANGELOG.md` under
  `[Unreleased]`. Items that grow into design discussions: promote to an ADR.

## Queued

### Remove inert telemetry members

- **SemVer:** Major
- **Trigger:** the next Domain major taken for another reason.
- **Noted:** 2026-10-25

All are public and none is used:

- `CacheTelemetry.CallerTag` and the `caller` parameter of `CacheTelemetry.RecordOperation` — not
  recorded since 5.1.0.
- `ConductorTelemetry.OperationSucceededTag` — not recorded since 5.1.0.
- `RemoteClient.ActivitySource` and the `activitySource` constructor parameter — stored and never
  read; spans come from the shared `RemoteServicesClient` source.

Delete outright and list each in that major's migration doc.

### Seconds and fully qualified `error.type` across all Cirreum meters

- **SemVer:** Minor
- **Trigger:** a platform-wide telemetry pass covering every Cirreum meter at once.
- **Noted:** 2026-10-25

Every Cirreum duration histogram records in `ms`, and `error.type` records `GetType().Name`. The
OpenTelemetry semantic conventions and the .NET built-in meters use `s` and the fully qualified
type name. Changing one package alone would trade a convention mismatch for an internal one, so
this moves with the other meters or not at all. A unit change ships under a new instrument name so
existing queries go empty rather than read 1000× off.
