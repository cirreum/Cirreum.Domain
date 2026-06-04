namespace Cirreum.Authorization;

using Cirreum.Security;
using FluentValidation;

/// <summary>
/// Base Authorizer of <see cref="IAuthorizableObject"/> objects.
/// </summary>
/// <typeparam name="TAuthorizableObject">The type of the <see cref="IAuthorizableObject"/> being authorized.</typeparam>
/// <remarks>
/// <para>
/// This abstract class provides a base implementation of the
/// <see cref="IAuthorizer{TAuthorizableObject}"/> interface and
/// serves as a foundational authorizer for types that implement the
/// <see cref="IAuthorizableObject"/> interface.
/// </para>
/// <para>
/// Inherit this rather than implementing <see cref="IAuthorizer{TAuthorizableObject}"/>
/// directly — the base supplies the common user-state / role / object validation rules.
/// Works with commands, queries, domain entities, or any type that implements
/// <see cref="IAuthorizableObject"/>:
/// </para>
/// <example>
/// <code>
/// // A command
/// public class CreateOrderAuthorizer : AuthorizerBase&lt;CreateOrderCommand&gt; { }
///
/// // A domain entity
/// public class OrderAuthorizer : AuthorizerBase&lt;Order&gt; { }
///
/// // Any authorizable type
/// public class ReportAuthorizer : AuthorizerBase&lt;AnalyticsReport&gt; { }
/// </code>
/// </example>
/// </remarks>
public abstract class AuthorizerBase<TAuthorizableObject>
	: AbstractValidator<AuthorizationContext<TAuthorizableObject>>
	, IAuthorizer<TAuthorizableObject>
	where TAuthorizableObject : IAuthorizableObject {

	/// <summary>
	/// Initializes a new instance of <see cref="AuthorizerBase{TAuthorizableObject}"/>
	/// </summary>
	protected AuthorizerBase() {

		this.RuleFor(context => context.UserState)
			.NotNull()
			.WithMessage("User state cannot be null");

		this.RuleFor(context => context.EffectiveRoles)
			.NotNull()
			.WithMessage("User roles cannot be null");

		this.RuleFor(context => context.AuthorizableObject)
			.NotNull()
			.WithMessage("Authorizable object cannot be null")
			.Must(obj => obj.GetType() == typeof(TAuthorizableObject))
			.WithMessage($"Authorizable object must be exactly of type {typeof(TAuthorizableObject).Name}, not a subclass");
	}

	/// <summary>
	/// Validates that the user has the specified role.
	/// </summary>
	/// <param name="role">The role to check.</param>
	protected void HasRole(Role role) {
		this.RuleFor(context => context.EffectiveRoles)
			.HasRole(role);
	}

	/// <summary>
	/// Must have at least one of the specified roles.
	/// </summary>
	/// <param name="roles">The roles to evaluate for inclusion.</param>
	protected void HasAnyRole(params Role[] roles) {
		this.RuleFor(context => context.EffectiveRoles)
			.HasAnyRole(roles);
	}

	/// <summary>
	/// Must have all roles specified.
	/// </summary>
	/// <param name="roles">The roles to evaluate for inclusion.</param>
	protected void HasAllRoles(params Role[] roles) {
		this.RuleFor(context => context.EffectiveRoles)
			.HasAllRoles(roles);
	}

	/// <summary>
	/// The user must have 2 or more roles.
	/// </summary>
	protected void HasTwoOrMoreRoles() {
		this.RuleFor(context => context.EffectiveRoles)
			.HasTwoOrMoreRoles();
	}

	/// <summary>
	/// The <see cref="IUserState.Principal"/> must have the specified <paramref name="claimType"/>
	/// the specified <paramref name="claimValue"/>.
	/// </summary>
	/// <param name="claimType">The type (name) of the claim.</param>
	/// <param name="claimValue">The value of the claim.</param>
	protected void HasClaim(string claimType, string claimValue) {
		this.RuleFor(context => context.UserState).HasClaim(claimType, claimValue);
	}

	/// <summary>
	/// Registers nested rules under a gate that applies them only when the operation
	/// declares the specified <paramref name="permission"/> via
	/// <see cref="RequiresGrantAttribute"/>.
	/// </summary>
	/// <param name="permission">The permission whose presence enables the nested rules.</param>
	/// <param name="configure">The action that defines the nested rules.</param>
	/// <returns>
	/// An <see cref="IConditionBuilder"/> that can be chained with <c>.Otherwise(...)</c>
	/// to register rules for the inverse case.
	/// </returns>
	/// <example>
	/// <code>
	/// this.WhenRequiresGrant(IssuePermissions.Delete, () =>
	///     this.HasAnyRole(Roles.IssueManager, Roles.IssueEscalator))
	/// .Otherwise(() =>
	///     this.HasRole(Roles.ReadOnlyUser));
	/// </code>
	/// </example>
	protected IConditionBuilder WhenRequiresGrant(Permission permission, Action configure) {
		ArgumentNullException.ThrowIfNull(permission);
		ArgumentNullException.ThrowIfNull(configure);
		return this.When(ctx => ctx.RequiredGrants.Contains(permission), configure);
	}

	/// <summary>
	/// Registers nested rules under a gate that applies them only when the operation
	/// declares any of the specified <paramref name="permissions"/> via
	/// <see cref="RequiresGrantAttribute"/>.
	/// </summary>
	/// <param name="permissions">The permissions to check — rules apply when any is present.</param>
	/// <param name="configure">The action that defines the nested rules.</param>
	/// <returns>
	/// An <see cref="IConditionBuilder"/> that can be chained with <c>.Otherwise(...)</c>
	/// to register rules for the inverse case.
	/// </returns>
	/// <example>
	/// <code>
	/// this.WhenRequiresAnyGrant([IssuePermissions.Delete, IssuePermissions.Archive], () =>
	///     this.HasRole(Roles.IssueManager))
	/// .Otherwise(() =>
	///     this.HasAnyRole(Roles.User, Roles.ReadOnlyUser));
	/// </code>
	/// </example>
	protected IConditionBuilder WhenRequiresAnyGrant(Permission[] permissions, Action configure) {
		ArgumentNullException.ThrowIfNull(permissions);
		ArgumentNullException.ThrowIfNull(configure);
		return this.When(ctx => ctx.RequiredGrants.ContainsAny(permissions), configure);
	}

	/// <summary>
	/// Registers nested rules under a gate that applies them only when the operation
	/// declares all of the specified <paramref name="permissions"/> via
	/// <see cref="RequiresGrantAttribute"/>.
	/// </summary>
	/// <param name="permissions">The permissions to check — rules apply when all are present.</param>
	/// <param name="configure">The action that defines the nested rules.</param>
	/// <returns>
	/// An <see cref="IConditionBuilder"/> that can be chained with <c>.Otherwise(...)</c>
	/// to register rules for the inverse case.
	/// </returns>
	/// <example>
	/// <code>
	/// this.WhenRequiresAllGrants([IssuePermissions.Delete, IssuePermissions.Admin], () =>
	///     this.HasRole(Roles.SuperAdmin));
	/// </code>
	/// </example>
	protected IConditionBuilder WhenRequiresAllGrants(Permission[] permissions, Action configure) {
		ArgumentNullException.ThrowIfNull(permissions);
		ArgumentNullException.ThrowIfNull(configure);
		return this.When(ctx => ctx.RequiredGrants.ContainsAll(permissions), configure);
	}

	/// <summary>
	/// Registers nested rules under a gate that applies them only when the operation
	/// does not declare the specified <paramref name="permission"/> via
	/// <see cref="RequiresGrantAttribute"/>.
	/// </summary>
	/// <param name="permission">The permission whose absence enables the nested rules.</param>
	/// <param name="configure">The action that defines the nested rules.</param>
	/// <returns>
	/// An <see cref="IConditionBuilder"/> that can be chained with <c>.Otherwise(...)</c>
	/// to register rules for the inverse case.
	/// </returns>
	/// <example>
	/// <code>
	/// this.UnlessRequiresGrant(IssuePermissions.Delete, () =>
	///     this.HasRole(Roles.ReadOnlyUser))
	/// .Otherwise(() =>
	///     this.HasAnyRole(Roles.IssueManager, Roles.IssueEscalator));
	/// </code>
	/// </example>
	protected IConditionBuilder UnlessRequiresGrant(Permission permission, Action configure) {
		ArgumentNullException.ThrowIfNull(permission);
		ArgumentNullException.ThrowIfNull(configure);
		return this.When(ctx => !ctx.RequiredGrants.Contains(permission), configure);
	}

	/// <summary>
	/// Registers nested rules under a gate that applies them only when the operation
	/// does not declare any of the specified <paramref name="permissions"/> via
	/// <see cref="RequiresGrantAttribute"/>.
	/// </summary>
	/// <param name="permissions">The permissions to check — rules apply when none are present.</param>
	/// <param name="configure">The action that defines the nested rules.</param>
	/// <returns>
	/// An <see cref="IConditionBuilder"/> that can be chained with <c>.Otherwise(...)</c>
	/// to register rules for the inverse case.
	/// </returns>
	protected IConditionBuilder UnlessRequiresAnyGrant(Permission[] permissions, Action configure) {
		ArgumentNullException.ThrowIfNull(permissions);
		ArgumentNullException.ThrowIfNull(configure);
		return this.When(ctx => !ctx.RequiredGrants.ContainsAny(permissions), configure);
	}

	/// <summary>
	/// Registers nested rules under a gate that applies them only when the operation
	/// does not declare all of the specified <paramref name="permissions"/> via
	/// <see cref="RequiresGrantAttribute"/>.
	/// </summary>
	/// <param name="permissions">The permissions to check — rules apply when not all are present.</param>
	/// <param name="configure">The action that defines the nested rules.</param>
	/// <returns>
	/// An <see cref="IConditionBuilder"/> that can be chained with <c>.Otherwise(...)</c>
	/// to register rules for the inverse case.
	/// </returns>
	protected IConditionBuilder UnlessRequiresAllGrants(Permission[] permissions, Action configure) {
		ArgumentNullException.ThrowIfNull(permissions);
		ArgumentNullException.ThrowIfNull(configure);
		return this.When(ctx => !ctx.RequiredGrants.ContainsAll(permissions), configure);
	}


}