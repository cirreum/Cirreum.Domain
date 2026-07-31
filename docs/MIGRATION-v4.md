# Cirreum.Domain v3 → v4 Migration

v4 carries one breaking change: **the grant-factory orchestrator no longer merges an implicit
home owner into the granted set.** Grant records are the only source of owner-scoped access
(paired with `Cirreum.Contracts` 4.0.0, which removes
`IOperationGrantProvider.ResolveHomeOwnerAsync` — the app-facing half of this change).

---

## ⚠️ Seed home grant rows BEFORE upgrading

This is a **behavioral** break, not just a compile-time one. Under v3, a caller whose
`ApplicationUser` implements `IOwnedApplicationUser` received unconditional access to their
home owner even with zero grant records. Under v4, zero qualifying records means `Denied` —
no exceptions. Upgrading a deployment without first seeding home grant rows fail-closes all
home-company access for tenant users.

The deploy-order walkthrough (seed → verify → upgrade) and the recommended company-self-grant
record shape live in `Cirreum.Contracts`' `MIGRATION-v4.md` — one guide for the pair.

## 1. Grant-factory home-owner merge — removed

| | Before | After |
|---|---|---|
| Cold-path resolution | `ResolveGrantsAsync` + `ResolveHomeOwnerAsync` + merge | `ResolveGrantsAsync` only |
| Empty granted set, home owner present | Access to the home owner | `OperationGrant.Denied` |
| Empty granted set, no home owner | `OperationGrant.Denied` | `OperationGrant.Denied` (unchanged) |

There is no code change in this package's consuming surface — the merge was internal to the
orchestrator. Apps interact with the change through the `Cirreum.Contracts` interface removal
and the seeding requirement above.

## What didn't change

- Owner-scope enforcement per operation shape (mutate/lookup/search/self), including
  `OwnerId` auto-stamping from a single-owner grant and Pattern C lookup deferral.
- The disabled-user backstop (`IOwnedApplicationUser.IsEnabled` denies before grants resolve).
- Bypass semantics (`ShouldBypassAsync`), denied/unrestricted translation, L1/L2 grant caching
  and `GrantsInvalidated` invalidation.
- Self-scoped operations — identity-based, never involved home semantics.

## Downstream package impact

Higher layers are repin-only. Consuming applications: see `Cirreum.Contracts` `MIGRATION-v4.md`.
