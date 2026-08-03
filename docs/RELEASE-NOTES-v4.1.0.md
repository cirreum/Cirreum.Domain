# Cirreum.Domain 4.1.0 — The Disabled-User Gate Moves, and Four Reason Values Change

## Why this release exists

A consuming application found workforce users denied `USER_DISABLED` on every grant-scoped
operation while role-gated admin pages on the same session worked fine. From the portal it read as
random: some screens loaded, some returned 403.

The gate that denied them asked whether the caller's application user was enabled. It got a `null`
application user — the correct outcome for a workforce caller, whose roles come from the identity
provider and who has no row in the application's user store — and read that as *disabled*.

Fixing the null case surfaced a second defect pointing the other way, and a coverage gap wider than
both.

## Read this first if you disable users

**Disabled users are now denied on every operation.** The check previously lived inside the grant
evaluator, which short-circuits for any operation that doesn't implement a grantable interface — so
`IsEnabled` was enforced on grant-scoped operations only. Nothing upstream compensated: the claims
transformer merges an application user's roles into the principal without consulting `IsEnabled`, so
a disabled user kept every role they had and sailed past the no-roles check.

The practical effect before this release: **a user you disabled in your own admin UI retained full
access to every role-gated operation in your application.** Only grant-scoped operations stopped
them.

Before upgrading, it's worth knowing which of your operations were relying on that — anything a
disabled user could reach yesterday and can't tomorrow is a behavior change you'll want to have
anticipated rather than discovered.

**If you disable users by stripping their roles**, the denial reason changes. That path used to
report `has no assigned roles`, because the no-roles check ran first. The disabled gate now runs
ahead of it, so the same user reports `User is disabled.` — the specific and actionable fact rather
than a side effect of how you implemented disabling.

## Read this first if you have dashboards

Four reason values change. They are telemetry, not API, so nothing fails to compile — a query bound
to the old value simply stops matching:

| Before | After |
|---|---|
| `unauthenticated` | `AUTHENTICATION_REQUIRED` |
| `no-roles` | `NO_ROLES_ASSIGNED` |
| `no-authorizers` | `NO_AUTHORIZERS_REGISTERED` |
| `error` | `EVALUATION_ERROR` |

`USER_DISABLED` is unchanged — it was already a `DenyCodes` value, and alerting bound to it keeps
working.

Two things get better in the same pass, both of which were quietly missing:

**The four checks above now reach `cirreum.authz.decisions`.** They previously recorded a duration
and nothing else, so the decisions counter had no record that a caller was ever turned away for
being unauthenticated, role-less, or hitting an unguarded operation. They now report
`cirreum.authz.stage = preflight` with a step of `authentication`, `application-user`, `roles`, or
`authorizer-presence`.

**Constraint, authorizer, policy, and grant denials now carry a reason on the span.** Only
`RecordDuration` tags the activity, and those paths passed a stage without a reason — so a denied
trace showed `decision=deny`, `stage=scope`, and no explanation. If you have ever opened a denial
span and found nothing telling you why, this is why.

## What the check is now

```csharp
if (userState.ApplicationUser is { IsEnabled: false }) {
    // 403 ForbiddenAccessException, DenyCodes.UserDisabled
}
```

Deny only on proof of disablement. No record → pass. A record of any shape reporting enabled → pass.
A record reporting disabled → deny.

Two things it deliberately no longer does. It doesn't consult application-user load state: "never
attempted" and "attempted, found nothing" are the same answer, and treating them as different is
what produced the original defect. And it doesn't test for `IOwnedApplicationUser` before reading
`IsEnabled` — that member is declared on `IApplicationUser`, so narrowing to the owned interface
silently excluded every application whose user type has no tenant dimension. If your user type
implements `IApplicationUser` directly, your disabled users were never being checked at all.

The gate also moved: it now runs in `DefaultAuthorizationEvaluator`, immediately after the
authentication check, rather than inside `OperationGrantEvaluator`. Disablement is a fact about the
caller, not about scope — and Stage 1 is now purely about scope, with no knowledge of
`IApplicationUser` in it.

## Compatibility

**No API change.** Nothing to edit; this release is behavior and telemetry.

Minor rather than patch because it denies callers who get through today — disabled users reaching
non-grantable operations. That is the fix, but it is a change in what your application permits, and
it deserved the version bump that says so.

Requires `Cirreum.Contracts` 4.1.0, which carries the `preflight` stage, its four steps, and the new
deny codes.

## See also

- [`CHANGELOG.md`](CHANGELOG.md)
- `Cirreum.Contracts` 4.1.0 — the telemetry vocabulary this release reports against
