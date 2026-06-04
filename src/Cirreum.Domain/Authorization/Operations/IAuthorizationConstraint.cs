namespace Cirreum.Authorization.Operations;

using FluentValidation.Results;

/// <summary>
/// Consumer-provided global authorization constraint. Stage 1, Step 1 of the
/// authorization pipeline — runs after the optional owner-scope gate (Grants)
/// but before per-operation authorizers and policies.
/// </summary>
/// <remarks>
/// <para>
/// Authorization constraints are application-supplied, cross-cutting pre-checks
/// evaluated against the <see cref="AuthorizationContext{TAuthorizableObject}"/>.
/// They are global (not tied to a specific operation type) and run early in the
/// pipeline, similar to policies but with short-circuit semantics.
/// </para>
/// <para>
/// Constraints do NOT know about override roles or
/// <see cref="IApplicationUser"/> — those concerns live only in
/// <see cref="Grants.OperationGrantEvaluator"/>.
/// </para>
/// <para>
/// Zero or more constraints may be registered. They run in registration order;
/// the first failure short-circuits Stage 1.
/// </para>
/// </remarks>
public interface IAuthorizationConstraint {

	/// <summary>
	/// Evaluates the constraint against the authorizable object in the given context.
	/// </summary>
	/// <typeparam name="TAuthorizableObject">The type of authorizable object being evaluated.</typeparam>
	/// <param name="context">The authorization context containing caller identity and the authorizable object.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A <see cref="ValidationResult"/> — empty (valid) to pass, or with failures to deny.</returns>
	Task<ValidationResult> EvaluateAsync<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> context,
		CancellationToken cancellationToken = default)
		where TAuthorizableObject : notnull, IAuthorizableObject;
}
