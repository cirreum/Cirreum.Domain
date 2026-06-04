namespace Cirreum.Authorization;

using Cirreum.Authorization.Diagnostics;
using Cirreum.Authorization.Operations;
using Cirreum.Authorization.Operations.Grants;
using Cirreum.Exceptions;
using Cirreum.Security;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Immutable;

/// <summary>
/// The default implementation of the <see cref="IAuthorizationEvaluator"/>.
/// Runs the three-stage authorization pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline is:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>Stage 1 — Scope</b>
/// <list type="bullet">
/// <item><description>
/// Step 0: grant evaluator (<see cref="OperationGrantEvaluator"/>, optional,
/// applies only to <see cref="IGrantableMutateBase"/>/<see cref="IGrantableLookupBase"/>/<see cref="IGrantableSearchBase"/>).
/// </description></item>
/// <item><description>
/// Step 1: authorization constraints (<see cref="IAuthorizationConstraint"/>, zero or more,
/// run in registration order).
/// </description></item>
/// </list>
/// First failure in Stage 1 short-circuits the pipeline.
/// </description></item>
/// <item><description>
/// <b>Stage 2 — Authorizer</b>: object authorizers (<see cref="IAuthorizer{TAuthorizableObject}"/>).
/// All authorizers run; failures are aggregated.
/// </description></item>
/// <item><description>
/// <b>Stage 3 — Policy</b>: policy validators (<see cref="IPolicyValidator"/>) whose
/// <see cref="IPolicyValidator.AppliesTo{TAuthorizableObject}(TAuthorizableObject, DomainRuntimeType, DateTimeOffset)"/>
/// returns true. Run in <see cref="IPolicyValidator.Order"/>; failures are aggregated.
/// </description></item>
/// </list>
/// </remarks>
/// <param name="registry">The authorization role registry for resolving effective roles.</param>
/// <param name="userAccessor">The accessor for retrieving current user state.</param>
/// <param name="contextAccessor">
/// Scoped accessor that receives the resolved <see cref="AuthorizationContext"/> after role
/// resolution, making it available to downstream consumers (e.g., <c>ResourceAccessEvaluator</c>).
/// </param>
/// <param name="services">The service provider for resolving validators.</param>
/// <param name="logger">The logger for authorization events.</param>
/// <param name="grantEvaluator">
/// Optional grant evaluator. When present and the authorizable object implements a Granted
/// interface (<see cref="IGrantableMutateBase"/>/<see cref="IGrantableLookupBase"/>/<see cref="IGrantableSearchBase"/>),
/// runs as Stage 1 Step 0.
/// </param>
sealed class DefaultAuthorizationEvaluator(
	IAuthorizationRoleRegistry registry,
	IUserStateAccessor userAccessor,
	IAuthorizationContextAccessor contextAccessor,
	IServiceProvider services,
	ILogger<DefaultAuthorizationEvaluator> logger,
	OperationGrantEvaluator? grantEvaluator = null
) : IAuthorizationEvaluator {

	/// <inheritdoc/>
	/// <remarks>
	/// Ad-hoc evaluation entry point. Retrieves user state from
	/// <see cref="IUserStateAccessor"/> and delegates to the context-aware overload.
	/// </remarks>
	public async ValueTask<Result> Evaluate<TAuthorizableObject>(
		TAuthorizableObject authorizableObject,
		CancellationToken cancellationToken = default)
		where TAuthorizableObject : IAuthorizableObject {

		var userState = await userAccessor.GetUserState().ConfigureAwait(false);
		return await this.Evaluate(authorizableObject, userState, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Context-aware evaluation entry point. Uses the provided <see cref="IUserState"/>
	/// to avoid a redundant <see cref="IUserStateAccessor.GetUserState"/> call.
	/// </remarks>
	public async ValueTask<Result> Evaluate<TAuthorizableObject>(
		TAuthorizableObject authorizableObject,
		IUserState userState,
		CancellationToken cancellationToken = default)
		where TAuthorizableObject : IAuthorizableObject {

		var objectRuntimeType = authorizableObject.GetType();
		var objectName = objectRuntimeType.Name;
		var objectCompileTimeType = typeof(TAuthorizableObject);

		using var activity = AuthorizationTelemetry.StartActivity(objectName);
		var startTimestamp = Timing.Start();

		// Check authentication
		if (!userState.IsAuthenticated) {
			//******************************************
			//
			// NOT AUTHENTICATED
			//
			//******************************************
			var ex = new UnauthenticatedAccessException("User is not authenticated.");

			logger.LogAuthorizingDenied(
				userState.Name,
				objectName,
				ex.Message);

			AuthorizationTelemetry.RecordDuration(
				activity, objectName,
				Timing.GetElapsedMilliseconds(startTimestamp),
				AuthorizationTelemetry.DecisionDeny,
				reason: "unauthenticated");

			return Result.Fail(ex);
		}

		// Check cancellation early
		cancellationToken.ThrowIfCancellationRequested();

		// Check if the runtime type matches the compile-time type
		if (objectRuntimeType != objectCompileTimeType) {
			throw new ArgumentException(
				$"Authorizable object must be a concrete type. Expected {objectCompileTimeType.Name} but got {objectRuntimeType.Name}. " +
				"Do not pass casted instances.",
				nameof(authorizableObject));
		}

		// MS.DI's GetServices<T>() materializes as T[] internally; cast to avoid a second
		// allocation from .ToList() / .ToArray(). Fallback to collection-expr copy if a custom
		// container ever returns a non-array shape. Call GetService<IEnumerable<T>>()! directly
		// to bypass GetRequiredService's null-guard + throw-helper — IEnumerable<T> is always
		// registered by MS.DI (empty array if no components).
		var rawConstraints = services.GetService<IEnumerable<IAuthorizationConstraint>>()!;
		var constraints = rawConstraints as IAuthorizationConstraint[] ?? [.. rawConstraints];

		var rawAuthorizer = services.GetService<IEnumerable<IAuthorizer<TAuthorizableObject>>>()!;
		var objectAuthorizers = rawAuthorizer as IAuthorizer<TAuthorizableObject>[] ?? [.. rawAuthorizer];

		// Policy runtime-type filter is deferred into the foreach below — combined with
		// AppliesTo so we walk the array once instead of materializing a filtered copy here
		// and then iterating again with Where().OrderBy().
		var rawPolicy = services.GetService<IEnumerable<IPolicyValidator>>()!;
		var policyAuthorizers = rawPolicy as IPolicyValidator[] ?? [.. rawPolicy];

		var grantGateApplies = grantEvaluator is not null
			&& (authorizableObject is IGrantableMutateBase
				|| authorizableObject is IGrantableLookupBase
				|| authorizableObject is IGrantableSearchBase
				|| authorizableObject is IGrantableSelfBase);

		if (constraints.Length == 0
			&& objectAuthorizers.Length == 0
			&& policyAuthorizers.Length == 0
			&& !grantGateApplies) {
			//******************************************
			//
			// OBJECT HAS NO AUTHORIZERS AND
			// RUNTIME HAS NO POLICY VALIDATORS
			// AND NO CONSTRAINTS APPLY
			//
			//******************************************
			var emptyAuthContainerEx = new InvalidOperationException(
				$"'{objectName}' has no authorizers or applicable policies.");
			logger.LogAuthorizingDenied(
				userState.Name,
				objectName,
				emptyAuthContainerEx.Message);

			AuthorizationTelemetry.RecordDuration(
				activity, objectName,
				Timing.GetElapsedMilliseconds(startTimestamp),
				AuthorizationTelemetry.DecisionDeny,
				reason: "no-authorizers");

			return Result.Fail(emptyAuthContainerEx);
		}

		// Check cancellation before entering validation logic
		cancellationToken.ThrowIfCancellationRequested();

		// Get the user's roles
		var roles = userState.Profile.Roles
			.Select(registry.GetRoleFromString)
			.OfType<Role>()
			.ToImmutableList();

		if (roles.Count == 0) {
			//******************************************
			//
			// USER HAS NO REGISTERED ROLES
			//
			//******************************************
			var noRolesEx = new ForbiddenAccessException(
				$"User '{userState.Name}' has no assigned roles.");

			logger.LogAuthorizingDenied(
				userState.Name,
				objectName,
				noRolesEx.Message);

			AuthorizationTelemetry.RecordDuration(
				activity, objectName,
				Timing.GetElapsedMilliseconds(startTimestamp),
				AuthorizationTelemetry.DecisionDeny,
				reason: "no-roles");

			return Result.Fail(noRolesEx);
		}

		// Check cancellation before entering validation logic
		cancellationToken.ThrowIfCancellationRequested();

		const string scopeTemplate = "Authorizing user '{UserName}' for '{ObjectName}'";
		using var logScope = logger.BeginScope(scopeTemplate, userState.Name, objectName);

		try {

			// Build effective roles ONCE
			var effectiveRoles = registry.GetEffectiveRoles(roles);

			// Build the canonical AuthorizationContext that validators will use
			var authorizationContext = new AuthorizationContext<TAuthorizableObject>(
				userState,
				effectiveRoles,
				authorizableObject);

			// Stamp the resolved caller identity so downstream consumers
			// (e.g. ResourceAccessEvaluator) can read it without re-resolving roles.
			contextAccessor.Set(authorizationContext);

			//******************************************
			//
			// STAGE 1 — SCOPE
			//
			// Step 0: owner-scope gate
			// Step 1: authorization constraints
			//
			// First failure short-circuits.
			//
			//******************************************

			if (grantGateApplies) {
				var grantResult = await grantEvaluator!
					.EvaluateAsync(authorizationContext, cancellationToken)
					.ConfigureAwait(false);

				if (!grantResult.IsValid) {
					// OperationGrantEvaluator already called RecordDecision() via EmitTelemetry()
					AuthorizationTelemetry.RecordDuration(
						activity, objectName,
						Timing.GetElapsedMilliseconds(startTimestamp),
						AuthorizationTelemetry.DecisionDeny,
						denyStage: AuthorizationTelemetry.StageScope);
					return this.DenyFromStage(grantResult.Errors, userState.Name, objectName);
				}
			}

			foreach (var constraint in constraints) {
				cancellationToken.ThrowIfCancellationRequested();

				var constraintResult = await constraint
					.EvaluateAsync(authorizationContext, cancellationToken)
					.ConfigureAwait(false);

				if (!constraintResult.IsValid) {
					AuthorizationTelemetry.RecordDecision(
						stage: AuthorizationTelemetry.StageScope,
						step: AuthorizationTelemetry.StepConstraint,
						decision: AuthorizationTelemetry.DecisionDeny,
						reason: constraintResult.Errors.FirstOrDefault()?.ErrorCode ?? "UNKNOWN",
						evaluator: constraint.GetType().Name,
						resourceType: objectName);
					AuthorizationTelemetry.RecordDuration(
						activity, objectName,
						Timing.GetElapsedMilliseconds(startTimestamp),
						AuthorizationTelemetry.DecisionDeny,
						denyStage: AuthorizationTelemetry.StageScope);
					return this.DenyFromStage(constraintResult.Errors, userState.Name, objectName);
				}
			}

			//******************************************
			//
			// STAGE 2 — OBJECT AUTHORIZERS
			//
			// Aggregate failures within the stage. If any object authorizer
			// denies, short-circuit before Stage 3 — policy checks are
			// irrelevant (and often expensive) once object-level access is
			// denied.
			//
			//******************************************

			// Create FluentValidation context
			var validationContext = new ValidationContext<AuthorizationContext<TAuthorizableObject>>(authorizationContext);

			List<ValidationFailure>? stageFailures = null;

			// Run the Object Authorizer. By contract each TAuthorizableObject has exactly one
			// AuthorizerBase<TAuthorizableObject> registered (mirrors AbstractValidator<T>
			// per T in FluentValidation). Extra registrations are a misconfiguration and
			// fail loud at evaluation time.
			if (objectAuthorizers.Length > 1) {
				throw new InvalidOperationException(
					$"Multiple IAuthorizer<{typeof(TAuthorizableObject).Name}> registrations detected "
					+ $"({objectAuthorizers.Length}). Exactly one AuthorizerBase<T> per "
					+ "authorizable type is the expected contract.");
			}
			if (objectAuthorizers.Length == 1
				&& objectAuthorizers[0] is AuthorizerBase<TAuthorizableObject> authorizer) {
				var authResult = await authorizer
					.ValidateAsync(validationContext, cancellationToken)
					.ConfigureAwait(false);
				foreach (var failure in authResult.Errors) {
					if (failure is not null) {
						(stageFailures ??= []).Add(failure);
					}
				}
			}

			if (stageFailures is not null) {
				AuthorizationTelemetry.RecordDecision(
					stage: AuthorizationTelemetry.StageResource,
					step: AuthorizationTelemetry.StepResourceAuthorizer,
					decision: AuthorizationTelemetry.DecisionDeny,
					reason: stageFailures[0].ErrorCode ?? "UNKNOWN",
					evaluator: objectAuthorizers[0].GetType().Name,
					resourceType: objectName);
				AuthorizationTelemetry.RecordDuration(
					activity, objectName,
					Timing.GetElapsedMilliseconds(startTimestamp),
					AuthorizationTelemetry.DecisionDeny,
					denyStage: AuthorizationTelemetry.StageResource);
				return this.DenyFromStage(stageFailures, userState.Name, objectName);
			}

			//******************************************
			//
			// STAGE 3 — POLICY VALIDATORS
			//
			// Aggregate failures within the stage.
			//
			//******************************************

			// Run applicable Policy Authorizers. Combine runtime-support + AppliesTo filters
			// in one pass; sort only the applicable subset.
			var applicablePolicies = FilterAndOrderPolicies(
				policyAuthorizers, authorizableObject, DomainContext.RuntimeType, authorizationContext.Timestamp);

			foreach (var policyValidator in applicablePolicies) {
				cancellationToken.ThrowIfCancellationRequested();

				var policyResult = await policyValidator
					.ValidateAsync(authorizationContext, cancellationToken)
					.ConfigureAwait(false);

				if (!policyResult.IsValid) {
					(stageFailures ??= []).AddRange(policyResult.Errors);
				}
			}

			if (stageFailures is not null) {
				AuthorizationTelemetry.RecordDecision(
					stage: AuthorizationTelemetry.StagePolicy,
					step: AuthorizationTelemetry.StepPolicyValidator,
					decision: AuthorizationTelemetry.DecisionDeny,
					reason: stageFailures[0].ErrorCode ?? "UNKNOWN",
					resourceType: objectName);
				AuthorizationTelemetry.RecordDuration(
					activity, objectName,
					Timing.GetElapsedMilliseconds(startTimestamp),
					AuthorizationTelemetry.DecisionDeny,
					denyStage: AuthorizationTelemetry.StagePolicy);
				return this.DenyFromStage(stageFailures, userState.Name, objectName);
			}

			//******************************************
			//
			// AUTHORIZED
			//
			//******************************************
			logger.LogAuthorizingAllowed(
				userState.Name,
				objectName);

			AuthorizationTelemetry.RecordDuration(
				activity, objectName,
				Timing.GetElapsedMilliseconds(startTimestamp),
				AuthorizationTelemetry.DecisionPass,
				reason: AuthorizationTelemetry.ReasonPass);

			return Result.Success;

		} catch (OperationCanceledException) {
			// Expected - let it propagate
			throw;

		} catch (Exception ex) {
			// Unexpected runtime errors during validation
			// (e.g., database failures, network issues in validators)
			logger.LogAuthorizingUnknownError(
				ex,
				userState.Name,
				objectName,
				ex.Message);

			AuthorizationTelemetry.RecordDuration(
				activity, objectName,
				Timing.GetElapsedMilliseconds(startTimestamp),
				AuthorizationTelemetry.DecisionDeny,
				reason: "error");

			return Result.Fail(ex);
		}
	}

	private static List<IPolicyValidator> FilterAndOrderPolicies<TAuthorizableObject>(
		IPolicyValidator[] all,
		TAuthorizableObject authorizableObject,
		DomainRuntimeType runtimeType,
		DateTimeOffset timestamp)
		where TAuthorizableObject : IAuthorizableObject {

		// Walk once, keep applicable, sort by Order. Typical sizes are small (2-8) so
		// List.Sort with the delegate comparer is cheaper than LINQ's OrderBy + stable sort.
		var applicable = new List<IPolicyValidator>(all.Length);
		foreach (var pv in all) {
			if (pv.SupportedRuntimeTypes.Contains(runtimeType)
				&& pv.AppliesTo(authorizableObject, runtimeType, timestamp)) {
				applicable.Add(pv);
			}
		}
		if (applicable.Count > 1) {
			applicable.Sort(static (a, b) => a.Order.CompareTo(b.Order));
		}
		return applicable;
	}

	private Result DenyFromStage(List<ValidationFailure> failures, string userName, string objectName) {
		var message = string.Join(',', failures.Select(f => f.ErrorMessage));
		logger.LogAuthorizingDenied(
			userName,
			objectName,
			message);
		return Result.Fail(new ForbiddenAccessException(message));
	}
}
