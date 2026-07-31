# Cirreum.Domain v2 → v3 Migration

v3 is the "policy authorizer" vocabulary correction. One concept is renamed across three
surfaces; there are no behavior changes. Every change is a compile error rather than a silent
behavior change.

"Validator" in Cirreum means FluentValidation property validation — the Conductor `Validation`
intercept's stage. The Stage 3 extension point performs **authorization**: it runs inside the
authorization evaluator, denies with `ForbiddenAccessException`, and participates in deny
telemetry. The framework's own registration code already called these types "policy authorizers";
the public surface now agrees.

---

## 1. `IPolicyValidator` → `IPolicyAuthorizer`

| Before | After |
|---|---|
| `IPolicyValidator` | `IPolicyAuthorizer` |
| `IPolicyValidator.ValidateAsync(...)` | `IPolicyAuthorizer.EvaluateAsync(...)` |

`PolicyName`, `Order`, `SupportedRuntimeTypes`, and `AppliesTo` are unchanged. `EvaluateAsync`
has the identical signature `ValidateAsync` had (context + cancellation token, returns
FluentValidation `ValidationResult`) — the method verb now matches its Stage 1 siblings
(`IAuthorizationConstraint.EvaluateAsync`, the grant evaluator's `EvaluateAsync`).

### Migration

Find/replace, then rename the method override:

```csharp
// Before
public sealed class MaintenanceWindowPolicy : IPolicyValidator {
    public Task<ValidationResult> ValidateAsync<T>(...) { ... }
}

// After
public sealed class MaintenanceWindowPolicy : IPolicyAuthorizer {
    public Task<ValidationResult> EvaluateAsync<T>(...) { ... }
}
```

Discovery, registration, ordering, and evaluation semantics are unchanged — implementations are
found in scanned assemblies and run in ascending `Order` exactly as before.

## 2. `AttributeValidatorBase<TAttribute>` → `AttributePolicyAuthorizerBase<TAttribute>`

The attribute-gated base was never a validator — it is an `IPolicyAuthorizer` base whose
`AppliesTo` keys on attribute presence. It also leaves the `Authorization.Validators` file folder
(which holds the genuine property validators: `HasRoleValidator`, `HasClaimValidator`, …); its
namespace was already `Cirreum.Authorization` and does not change.

### Migration

```csharp
// Before
public sealed class RequiresApprovalPolicy : AttributeValidatorBase<RequiresApprovalAttribute> {
    public override Task<ValidationResult> ValidateAsync<T>(...) { ... }
}

// After
public sealed class RequiresApprovalPolicy : AttributePolicyAuthorizerBase<RequiresApprovalAttribute> {
    public override Task<ValidationResult> EvaluateAsync<T>(...) { ... }
}
```

## 3. Telemetry step (via Cirreum.Contracts 3.0.0)

The Stage 3 deny telemetry step is emitted as `policy-authorizer` (was `policy-validator`),
following `AuthorizationTelemetry.StepPolicyValidator` → `StepPolicyAuthorizer` in
`Cirreum.Contracts` 3.0.0. Update dashboards/alerts filtering on that dimension value; see
Contracts' `MIGRATION-v3.md`. The stage value (`policy`) is unchanged.

---

## What didn't change

- Stage 2 object authorizers: `AuthorizerBase<T>` / `IAuthorizer<T>` and the `Has*` rule surface.
- The property validators in `Cirreum.Authorization.Validators`.
- Stage 1: constraints, the grant gate, owner stamping.
- The Conductor pipeline order restored in 2.0.1
  (`Validation → Authorization → GrantedLookupAudit → [custom] → HandlerPerformance → QueryCaching`).
- All evaluation semantics, aggregation, ordering, and deny behavior.

## Downstream package impact

- `Cirreum.Contracts` 3.0.0 — the paired telemetry-constant rename (this package re-pins it).
- `Cirreum.Introspection` — reflects over the Stage 3 surface; requires its paired update.
- All higher-layer packages re-pin without source changes unless they implement a policy
  authorizer.
