namespace Cirreum.Authorization.Operations.Grants;

using Cirreum.Caching;
using Cirreum.Conductor;
using Cirreum.Security;
using FluentValidation.Results;
using System.Diagnostics;

/// <summary>
/// Stage 1 / Step 0 — the grant-aware scope gate. Evaluates the caller's
/// <see cref="OperationGrant"/> via the registered <see cref="IOperationGrantFactory"/> and
/// enforces grant timing:
/// <list type="bullet">
///   <item><description><b>Mutate:</b> <c>OwnerId ∈ grant</c> before handler.</description></item>
///   <item><description><b>Lookup:</b> stash grant for post-fetch check (Pattern C), or enforce <c>OwnerId ∈ grant</c> when supplied.</description></item>
///   <item><description><b>Search:</b> <c>OwnerIds ⊆ grant</c>, stamp when null.</description></item>
///   <item><description><b>Self:</b> <c>ExternalId == context.UserId</c> identity match; admin bypass via <see cref="IOperationGrantProvider.ShouldBypassAsync"/>.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Authorizable objects that do not implement <see cref="IGrantableMutateBase"/>, <see cref="IGrantableLookupBase"/>,
/// <see cref="IGrantableSearchBase"/>, or <see cref="IGrantableSelfBase"/> short-circuit with a pass —
/// the gate is purely a Grants concern.
/// </para>
/// <para>
/// All customization is in <see cref="IOperationGrantProvider"/>: bypass logic, grant lookup,
/// and home-owner policy. This evaluator is sealed with no virtual extension points.
/// </para>
/// </remarks>
public sealed class OperationGrantEvaluator(
	IOperationGrantFactory grantFactory,
	IOperationGrantAccessor grantAccessor,
	CacheKeyContext cacheKeyContext) {

	/// <summary>
	/// Evaluates the grant-aware scope gate against the authorizable object in the given context.
	/// </summary>
	public async Task<ValidationResult> EvaluateAsync<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> context,
		CancellationToken cancellationToken = default)
		where TAuthorizableObject : notnull, IAuthorizableObject {

		// Not applicable — short-circuit pass.
		var mutate = context.AuthorizableObject as IGrantableMutateBase;
		var lookup = context.AuthorizableObject as IGrantableLookupBase;
		var search = context.AuthorizableObject as IGrantableSearchBase;
		var self = context.AuthorizableObject as IGrantableSelfBase;
		if (mutate is null && lookup is null && search is null && self is null) {
			return Pass();
		}

		// IsEnabled progressive check
		if (!CheckApplicationUserEnabled(context)) {
			EmitTelemetry(context, DenyCodes.UserDisabled);
			return Deny(DenyCodes.UserDisabled, "User is disabled.");
		}

		// Self-scoped: identity match — fast path without grant resolution.
		if (self is not null) {
			return await this.EvaluateSelfAsync(context, self, cancellationToken)
				.ConfigureAwait(false);
		}

		// Owner-scoped: grant resolution required.
		var grant = await grantFactory
			.CreateAsync(context, cancellationToken)
			.ConfigureAwait(false);

		grantAccessor.Set(grant);

		if (grant.IsDenied) {
			EmitTelemetry(context, DenyCodes.GrantDenied);
			return Deny(DenyCodes.GrantDenied, "Caller has no granted access for this operation.");
		}

		var result = search is not null
			? EvaluateSearch(search, grant)
			: mutate is not null
			? this.EvaluateMutate(context, mutate, grant)
			: this.EvaluateLookup(context, lookup!, grant);

		EmitTelemetry(context, result.IsValid ? AuthorizationTelemetry.ReasonPass : DenyReason(result));
		return result;
	}

	// Self-scoped enforcement —————————————————————————————————

	private async Task<ValidationResult> EvaluateSelfAsync<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> context,
		IGrantableSelfBase self,
		CancellationToken cancellationToken)
		where TAuthorizableObject : notnull, IAuthorizableObject {

		// Enrichment: auto-stamp Id from caller identity when null.
		if (self.Id is null) {
			self.Id = context.UserId;
			EmitTelemetry(context, AuthorizationTelemetry.ReasonPass, AuthorizationTelemetry.StepSelfIdentity);
			return Pass();
		}

		if (!self.IsValidId) {
			EmitTelemetry(context, DenyCodes.ResourceIdRequired, AuthorizationTelemetry.StepSelfIdentity);
			return Deny(DenyCodes.ResourceIdRequired, "Id is required and must be valid on self-scoped resources.");
		}

		// Fast path: identity match — no factory needed.
		if (string.Equals(self.ExternalId, context.UserId, StringComparison.OrdinalIgnoreCase)) {
			EmitTelemetry(context, AuthorizationTelemetry.ReasonPass, AuthorizationTelemetry.StepSelfIdentity);
			return Pass();
		}

		// Non-match: check for admin/privilege bypass via grant resolution.
		var grant = await grantFactory
			.CreateAsync(context, cancellationToken)
			.ConfigureAwait(false);

		if (grant.IsUnrestricted) {
			EmitTelemetry(context, AuthorizationTelemetry.ReasonPass, AuthorizationTelemetry.StepSelfIdentity);
			return Pass();
		}

		EmitTelemetry(context, DenyCodes.NotResourceOwner, AuthorizationTelemetry.StepSelfIdentity);
		return Deny(DenyCodes.NotResourceOwner, "Caller is not the resource owner and does not have bypass privileges.");
	}

	// Owner-scoped enforcement ————————————————————————————————

	private ValidationResult EvaluateMutate<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> context,
		IGrantableMutateBase mutate,
		OperationGrant grant) where TAuthorizableObject : notnull, IAuthorizableObject {

		if (mutate.OwnerId.HasValue()) {
			return grant.Contains(mutate.OwnerId!)
				? Pass()
				: Deny(DenyCodes.OwnerNotInReach, "Requested owner is not in the caller's granted permissions.");
		}

		// OwnerId is null — enforce presence in grant for bounded grants.
		// Unrestricted grants are exempt since they have no owner bounds.
		if (context.AuthenticationBoundary == AuthenticationBoundary.Global) {
			return Deny(DenyCodes.OwnerIdRequired, "OwnerId is required for cross-tenant writes.");
		}
		// For tenant-scoped auth, require OwnerId when grant is not unrestricted to prevent mistakes.
		if (grant.IsUnrestricted) {
			return Deny(DenyCodes.OwnerIdRequired, "OwnerId is required — caller's grant is unrestricted.");
		}

		// Single owner in grant — auto-stamp and pass.
		// Surface the framework's inference via the grant accessor + activity tag so
		// downstream consumers can distinguish caller-supplied OwnerId from auto-stamped.
		if (grant.OwnerIds is { Count: 1 }) {
			mutate.OwnerId = grant.OwnerIds[0];
			grantAccessor.MarkOwnerAutoStamped();
			Activity.Current?.SetTag(AuthorizationTelemetry.OwnerAutoStampedTag, true);
			return Pass();
		}

		// Multiple owners — ambiguous, require explicit OwnerId to prevent mistakes.
		return Deny(DenyCodes.OwnerAmbiguous, "OwnerId is required — caller's grant contains multiple owners.");

	}

	private ValidationResult EvaluateLookup<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> context,
		IGrantableLookupBase lookup,
		OperationGrant grant)
		where TAuthorizableObject : notnull, IAuthorizableObject {

		if (!string.IsNullOrWhiteSpace(lookup.OwnerId)) {
			if (!grant.Contains(lookup.OwnerId!)) {
				return Deny(DenyCodes.OwnerNotInReach, "Requested owner is not in the caller's granted access.");
			}
			this.StampCacheKeyContext(context, lookup);
			return Pass();
		}

		// Cacheable lookup: Global callers MUST supply OwnerId to prevent unbounded cache bucket.
		if (context.AuthorizableObject is ICacheableOperation) {
			if (context.AuthenticationBoundary == AuthenticationBoundary.Global) {
				return Deny(DenyCodes.CacheableReadOwnerIdRequired,
					"OwnerId is required for cross-tenant cacheable lookups.");
			}
		}

		// OwnerId null — defer to handler (Pattern C). Grant already stashed on accessor.
		this.StampCacheKeyContext(context, lookup);
		return Pass();
	}

	private static ValidationResult EvaluateSearch(
		IGrantableSearchBase search,
		OperationGrant grant) {
		if (search.OwnerIds is null) {
			// Stamp grant. Unrestricted = null (no bound).
			search.OwnerIds = grant.IsUnrestricted ? null : grant.OwnerIds;
			return Pass();
		}
		if (!grant.ContainsAll(search.OwnerIds)) {
			return Deny(DenyCodes.OwnerNotInReach, "One or more requested owners are not in the caller's granted access.");
		}
		return Pass();
	}

	// Application user guard ————————————————————————————————

	private static bool CheckApplicationUserEnabled<TAuthorizableObject>(AuthorizationContext<TAuthorizableObject> ctx)
		where TAuthorizableObject : notnull, IAuthorizableObject {

		if (!ctx.UserState.IsApplicationUserLoaded) {
			return true;
		}
		return ctx.UserState.ApplicationUser is IOwnedApplicationUser { IsEnabled: true };
	}

	// Cache key context ————————————————————————————————————————

	private void StampCacheKeyContext<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> context,
		IGrantableLookupBase lookup)
		where TAuthorizableObject : notnull, IAuthorizableObject {

		if (context.AuthorizableObject is not ICacheableOperation) {
			return;
		}

		var boundary = context.AuthenticationBoundary;
		cacheKeyContext.SetPrefix($"owner:{lookup.OwnerId}:boundary:{boundary}");
		cacheKeyContext.SetExtraTags([$"owner:{lookup.OwnerId}"]);
	}

	// Helpers ————————————————————————————————————————————————

	private static ValidationResult Pass() => new();

	private static ValidationResult Deny(string code, string message)
		=> new([new ValidationFailure(propertyName: code, errorMessage: message) {
			ErrorCode = code
		}]);

	private static string DenyReason(ValidationResult r)
		=> r.Errors.FirstOrDefault()?.ErrorCode ?? "UNKNOWN";

	private static void EmitTelemetry<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> ctx,
		string outcome,
		string step = AuthorizationTelemetry.StepOwnerScope)
		where TAuthorizableObject : notnull, IAuthorizableObject {

		var isPass = outcome == AuthorizationTelemetry.ReasonPass;
		var decision = isPass
			? AuthorizationTelemetry.DecisionPass
			: AuthorizationTelemetry.DecisionDeny;
		var boundary = ctx.AuthenticationBoundary.ToString().ToLowerInvariant();
		var resourceType = ctx.AuthorizableObject.GetType().Name;

		var activity = Activity.Current;
		if (activity is not null) {
			activity.SetTag(AuthorizationTelemetry.StageTag, AuthorizationTelemetry.StageScope);
			activity.SetTag(AuthorizationTelemetry.StepTag, step);
			activity.SetTag(AuthorizationTelemetry.ScopeTag, boundary);
			activity.SetTag(AuthorizationTelemetry.DecisionTag, decision);
			activity.SetTag(AuthorizationTelemetry.ReasonTag, outcome);
			activity.SetTag(AuthorizationTelemetry.EvaluatorTag, nameof(OperationGrantEvaluator));
			activity.SetTag(AuthorizationTelemetry.ResourceTypeTag, resourceType);
		}

		AuthorizationTelemetry.RecordDecision(
			stage: AuthorizationTelemetry.StageScope,
			step: step,
			decision: decision,
			reason: outcome,
			scope: boundary,
			evaluator: nameof(OperationGrantEvaluator),
			resourceType: resourceType);
	}
}
