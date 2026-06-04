namespace Cirreum.Authorization;

using Cirreum.Authorization.Validators;
using Cirreum.Security;
using FluentValidation;

/// <summary>
/// Extension methods for validators to use the custom validators.
/// </summary>
public static class AuthorizationValidatorExtensions {

	public static IRuleBuilderOptions<T, IEnumerable<Role>> HasRole<T>(
		this IRuleBuilder<T, IEnumerable<Role>> ruleBuilder, Role role) {
		return ruleBuilder.SetValidator(new HasRoleValidator<T>(role));
	}

	public static IRuleBuilderOptions<T, IEnumerable<Role>> HasAnyRole<T>(
		this IRuleBuilder<T, IEnumerable<Role>> ruleBuilder, params Role[] roles) {
		return ruleBuilder.SetValidator(new HasAnyRoleValidator<T>(roles));
	}

	public static IRuleBuilderOptions<T, IEnumerable<Role>> HasAllRoles<T>(
		this IRuleBuilder<T, IEnumerable<Role>> ruleBuilder, params Role[] roles) {
		return ruleBuilder.SetValidator(new HasAllRolesValidator<T>(roles));
	}

	public static IRuleBuilderOptions<T, IEnumerable<Role>> HasTwoOrMoreRoles<T>(
		this IRuleBuilder<T, IEnumerable<Role>> ruleBuilder) {
		return ruleBuilder.SetValidator(new HasTwoOrMoreRolesValidator<T>());
	}

	/// <summary>
	/// Validates that the user state has a specific claim with a specific value.
	/// </summary>
	/// <typeparam name="T">The type being validated</typeparam>
	/// <param name="ruleBuilder">The rule builder</param>
	/// <param name="claimType">The type of the claim</param>
	/// <param name="claimValue">The value of the claim</param>
	/// <returns>Rule builder options for chaining</returns>
	public static IRuleBuilderOptions<T, IUserState> HasClaim<T>(
		this IRuleBuilder<T, IUserState> ruleBuilder,
		string claimType,
		string claimValue) {
		return ruleBuilder.SetValidator(new HasClaimValidator<T>(claimType, claimValue));
	}

}
