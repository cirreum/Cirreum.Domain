namespace Cirreum.Authorization.Validators;

using FluentValidation;
using FluentValidation.Validators;

/// <summary>
/// Validates that a user has two or more roles.
/// </summary>
public class HasTwoOrMoreRolesValidator<T> : PropertyValidator<T, IEnumerable<Role>> {

	/// <inheritdoc/>
	public override string Name => "HasTwoOrMoreRolesValidator";

	/// <inheritdoc/>
	protected override string GetDefaultMessageTemplate(string errorCode)
		=> "Must have 2 or more roles";

	/// <inheritdoc/>
	public override bool IsValid(ValidationContext<T> context, IEnumerable<Role> value) {
		return value != null && value.Count() > 1;
	}

}