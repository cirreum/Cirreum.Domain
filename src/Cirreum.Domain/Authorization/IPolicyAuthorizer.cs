namespace Cirreum.Authorization;

using FluentValidation.Results;

/// <summary>
/// App-provided extension point for cross-cutting, policy-based authorization of
/// <see cref="IAuthorizableObject"/> instances — Stage 3 of the authorization pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Policy authorizers express operational authorization rules that span many operations —
/// time windows, runtime-environment restrictions, attribute-gated policies, tenant-wide
/// switches — as opposed to Stage 2 object authorizers (<see cref="IAuthorizer{TAuthorizableObject}"/>),
/// which carry the rules of a single authorizable type. Implementations decide their own
/// applicability per object and context via
/// <see cref="AppliesTo{TAuthorizableObject}(TAuthorizableObject, DomainRuntimeType, DateTimeOffset)"/>.
/// </para>
/// <para>
/// Implementations are discovered from scanned assemblies and run by the authorization
/// evaluator in ascending <see cref="Order"/>; failures aggregate and deny the dispatch.
/// Despite evaluating through FluentValidation's <see cref="ValidationResult"/>, this is an
/// authorization surface, not property validation — property validation is the Conductor
/// <c>Validation</c> intercept's stage.
/// </para>
/// </remarks>
public interface IPolicyAuthorizer {

	/// <summary>
	/// Gets the unique name that identifies this authorization policy.
	/// </summary>
	/// <value>
	/// A string that uniquely identifies the policy implemented by this authorizer.
	/// Used for registration, lookup, and diagnostic purposes.
	/// </value>
	string PolicyName { get; }

	/// <summary>
	/// Gets the execution priority order for this authorizer relative to other policy authorizers.
	/// </summary>
	/// <value>
	/// An integer representing the execution order, where lower values indicate higher priority
	/// and earlier execution. Policy authorizers run in ascending order of this value.
	/// </value>
	int Order { get; }

	/// <summary>
	/// Gets the application runtime types this authorizer is designed to operate within.
	/// </summary>
	/// <value>
	/// An array of <see cref="DomainRuntimeType"/> values specifying the runtime environments
	/// where this authorizer is applicable and should be executed.
	/// </value>
	DomainRuntimeType[] SupportedRuntimeTypes { get; }

	/// <summary>
	/// Determines whether this authorizer should be applied to the specified authorizable object
	/// within the given execution context.
	/// </summary>
	/// <typeparam name="TAuthorizableObject">
	/// The type of the authorizable object being evaluated. Must be non-nullable and implement <see cref="IAuthorizableObject"/>.
	/// </typeparam>
	/// <param name="authorizableObject">
	/// The <see cref="IAuthorizableObject"/> instance to evaluate for applicability.
	/// Cannot be <see langword="null"/>.
	/// </param>
	/// <param name="runtimeType">
	/// The application runtime type in which the authorization is being evaluated.
	/// </param>
	/// <param name="timestamp">
	/// The timestamp of when the authorization check is occurring, useful for time-based policies.
	/// </param>
	/// <returns>
	/// <see langword="true"/> if this authorizer should be applied to the specified object within
	/// the given context; otherwise, <see langword="false"/>.
	/// </returns>
	/// <remarks>
	/// This method allows policy authorizers to conditionally apply their logic based on object
	/// characteristics, runtime type, timestamp, or other environmental factors. Implementations
	/// should return <see langword="true"/> only when the policy is relevant to the provided
	/// object and context combination.
	/// </remarks>
	bool AppliesTo<TAuthorizableObject>(
		TAuthorizableObject authorizableObject,
		DomainRuntimeType runtimeType,
		DateTimeOffset timestamp)
		where TAuthorizableObject : notnull, IAuthorizableObject;

	/// <summary>
	/// Asynchronously evaluates the policy against an <see cref="IAuthorizableObject"/> within
	/// the specified authorization context.
	/// </summary>
	/// <typeparam name="TAuthorizableObject">
	/// The type of the authorizable object being authorized. Must be non-nullable and implement <see cref="IAuthorizableObject"/>.
	/// </typeparam>
	/// <param name="context">
	/// The authorization context containing the authorizable object and all necessary information
	/// for performing the evaluation. Cannot be <see langword="null"/>.
	/// </param>
	/// <param name="cancellationToken">
	/// A cancellation token that can be used to cancel the evaluation.
	/// Defaults to <see cref="CancellationToken.None"/>.
	/// </param>
	/// <returns>
	/// A <see cref="Task{ValidationResult}"/> representing the asynchronous evaluation.
	/// The result indicates whether authorization was granted, along with any errors or
	/// additional diagnostic information.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method performs the core authorization logic for the policy. Implementations should
	/// examine the provided context and apply policy-specific rules to determine whether the
	/// requested access should be authorized.
	/// </para>
	/// <para>
	/// The authorization context provides access to the caller's <see cref="Security.IUserState"/>,
	/// runtime type, timestamp, and other execution details. Policy authorizers can use these to
	/// implement sophisticated authorization logic.
	/// </para>
	/// <para>
	/// A successful evaluation (indicated by <see cref="ValidationResult.IsValid"/> being
	/// <see langword="true"/>) means the policy permits the requested access. A failed evaluation
	/// should include descriptive error information in the <see cref="ValidationResult.Errors"/>
	/// collection.
	/// </para>
	/// </remarks>
	Task<ValidationResult> EvaluateAsync<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> context,
		CancellationToken cancellationToken = default)
		where TAuthorizableObject : notnull, IAuthorizableObject;
}
