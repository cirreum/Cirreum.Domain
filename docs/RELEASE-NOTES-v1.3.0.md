# Cirreum.Domain 1.3.0 — every client gets a display name

## Why this release exists

`UserProfile.DisplayName` was a slot only one enrichment path ever filled: Microsoft Graph, in
`Cirreum.Runtime.Wasm.Msal`. Every other client — a bare-claims composition, or any
`Cirreum.Runtime.Wasm.Oidc` app (Descope, generic OIDC) — got a permanently-`null`
`DisplayName`, no matter what the token carried. This release moves a claims-level fallback
into the enricher every client installs by default, so a display name is available from claims
alone.

## What's new

`ClaimsUserProfileEnricher` gains a `DisplayName` consolidation step, called right after the
existing claim processing (so `Nickname`, `GivenName`, and `FamilyName` are already resolved):

```csharp
private static void ProcessDisplayName(UserProfile profile, ClaimsIdentity identity) {
    profile.DisplayName ??= profile.Nickname.NullIfWhiteSpace()
        ?? identity.FindFirst("name")?.Value.NullIfWhiteSpace()
        ?? JoinNames(profile.GivenName, profile.FamilyName);
}
```

The precedence: `Nickname` (the `nickname` claim) first, then the `name` claim, then a
`GivenName` + `FamilyName` composite for tokens that carry name parts but no whole-name claim.
`Nickname` goes first deliberately — `UserProfile.Name` is already resolved from the `name`
claim at construction (`ClaimsHelper.ResolveName`), so trying `name` first here would make
`DisplayName` redundant with `Name` whenever both claims are present. `Nickname` is the more
casual, more genuinely "display"-appropriate value, and the one that actually differentiates
the two properties.

**Fill-only, so richer enrichment still wins.** The `??=` means this only ever writes into an
empty `DisplayName`. A provider-specific enrichment — Microsoft Graph today, any future one —
can still overwrite with its own, richer value, whether it runs before or after this step.
Correctness never depends on which `IUserProfileEnricher` implementations are installed or in
what order.

## How it pairs with provisioned identity claims

This is the terminal step of the provisioning chain `Cirreum.IdentityProvider 2.0.0` and
`Cirreum.Runtime.Wasm 1.1.0` shipped: a provisioner mints `IdentityClaim.Name(DisplayName)` →
the wire carries it as `customName` → the client's built-in `CustomClaimCanonicalizer` aliases
it to a native `name` claim → this consolidation fills `UserProfile.DisplayName`. An app binds
one property; nothing else is required.

## Compatibility

- No API shape change — `UserProfile.DisplayName` was already a public, settable property.
- **This is a behavior change**, not a pure bug fix: `DisplayName` was previously reliably
  `null` for any client without Graph enrichment. An app that treated `null` as "no display
  name, fall back myself" now more often sees a populated value. Believed to be a strict
  improvement for every real consumer.

## See also

- `Cirreum.Runtime.Wasm.Msal` — simplifies its own Graph-enrichment `DisplayName` fallback to
  match (`graphUser.DisplayName ?? profile.DisplayName`), in the same release as this pairs
  with.
- `Cirreum.IdentityProvider 2.0.0` / `Cirreum.Runtime.Wasm 1.1.0` — the provisioning-claims
  chain this closes the loop for.
