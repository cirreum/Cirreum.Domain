namespace Cirreum.Authorization.Validators;

using Cirreum.Security;
using FluentValidation;
using FluentValidation.Validators;

/// <summary>
/// Validates that a user state has a specific claim with a specific value.
/// </summary>
/// <typeparam name="T">The type being validated</typeparam>
public class HasClaimValidator<T>(
	string claimType,
	string claimValue
) : PropertyValidator<T, IUserState> {

	/// <inheritdoc/>
	public override string Name => "HasClaimValidator";

	/// <inheritdoc/>
	protected override string GetDefaultMessageTemplate(string errorCode)
		=> $"Claim {claimType} with value {claimValue} not found";

	/// <inheritdoc/>
	public override bool IsValid(ValidationContext<T> context, IUserState userState) {
		return userState != null &&
			   userState.Principal != null &&
			   userState.Principal.Claims != null &&
			   userState.Principal.Claims.Any(c => c.Type == claimType && c.Value == claimValue);
	}

}