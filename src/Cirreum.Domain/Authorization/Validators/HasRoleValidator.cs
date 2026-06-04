namespace Cirreum.Authorization.Validators;

using FluentValidation;
using FluentValidation.Validators;

/// <summary>
/// Validates that a user has a specific role.
/// </summary>
public class HasRoleValidator<T>(
	Role role
) : PropertyValidator<T, IEnumerable<Role>> {

	/// <inheritdoc/>
	public override string Name => "HasRoleValidator";

	/// <inheritdoc/>
	protected override string GetDefaultMessageTemplate(string errorCode)
		=> $"Must have role '{role}'";

	/// <inheritdoc/>
	public override bool IsValid(ValidationContext<T> context, IEnumerable<Role> value) {
		return value != null && value.Contains(role);
	}

}