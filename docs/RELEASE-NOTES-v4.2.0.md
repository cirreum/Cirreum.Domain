# Cirreum.Domain 4.2.0 — the bootstrap route, and honest denial messages

## Why this release exists

A WebAssembly client cannot tell a user they are disabled, because reading the record that says
so has always required not being disabled: the client resolved its application user through its
own authorizable operations, the disabled gate denied them, and the router fell through to the
"not provisioned" screen with an error toast on top. The fix is a framework-owned bootstrap
endpoint that requires authentication and nothing else — never dispatched through the operation
pipeline, so no authorization gate stands between a disabled caller and the record describing
their state.

This release carries the piece both ends share: the route. The server mapping and the client
registration ship in the paired `Cirreum.Runtime.Server` / `Cirreum.Runtime.Wasm` releases.

Alongside it, a straight bug fix on the denial path itself.

## What's new

**`ApplicationUserEndpoint.Route`** — `/_cirreum/application-user`, in `Cirreum.RemoteServices`.
The server host maps it; the WebAssembly client calls it; sharing the constant means the two
ends cannot drift. The `/_cirreum/` prefix is the framework's reserved route namespace — app
routes cannot collide with it, and future framework endpoints become siblings under it.

**401/403 responses no longer surface raw JSON as the exception message.** `RemoteClient` read
denial bodies as plain strings, so in production `ForbiddenAccessException.Message` was a
problem-details blob:

```json
{"type":"…","title":"Forbidden","status":403,"detail":"Access denied","traceId":"…"}
```

Any app that surfaced `ex.Message` rendered that to a user. Denial bodies carrying a JSON
content type are now parsed and the safe `Detail` used; non-JSON bodies — typically empty, from
authentication middleware — fall back to the raw string, then the reason phrase. The special
case existed because middleware denials have empty, non-JSON bodies; the fix keeps that path
working rather than removing the distinction.

## Compatibility

Fully additive plus one message-content fix. `RemoteClient`'s contract is unchanged — 401/403
still throw (`UnauthenticatedAccessException` / `ForbiddenAccessException`); only the exception
*message* changes, from a JSON blob to the problem-details `Detail`. Code that parsed the raw
JSON out of `ex.Message` (none known) would need to stop; code that displayed it gets the
readable message it always intended.

## Coordinated downstream work

| Repo | Work |
|---|---|
| `Cirreum.Runtime.Server` | maps `GET /_cirreum/application-user` automatically when a server-side `IApplicationUserResolver` is registered |
| `Cirreum.Runtime.Wasm` | replaces the client-side resolver registration with `AddApplicationUser<TUser>(Uri)` calling this route |
| `Cirreum.Runtime.Wasm.Msal` / `.Oidc` | wrapper verbs over the new registration |

## See also

- `docs/CHANGELOG.md` — the enumerated changes
