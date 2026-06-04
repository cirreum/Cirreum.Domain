namespace Cirreum.Authorization.Validators;

using FluentValidation;
using FluentValidation.Validators;

/// <summary>
/// Validates that a user has at least one of the specified roles.
/// </summary>
public class HasAnyRoleValidator<T>(
	params Role[] roles
) : PropertyValidator<T, IEnumerable<Role>> {

	/// <inheritdoc/>
	public override string Name => "HasAnyRoleValidator";

	/// <inheritdoc/>
	protected override string GetDefaultMessageTemplate(string errorCode)
		=> $"Must have at least one of the following roles: {MessageFormatting.FormatRoleList(roles)}";

	/// <inheritdoc/>
	public override bool IsValid(ValidationContext<T> context, IEnumerable<Role> value) {
		return value != null && roles.Any(value.Contains);
	}

	static class MessageFormatting {
		/// <summary>
		/// Creates a natural-sounding list with proper "or" placement for role lists
		/// </summary>
		public static string FormatRoleList(IEnumerable<Role> roles) {
			if (roles == null) {
				return string.Empty;
			}

			// Filter out nulls and convert items to strings
			var filteredRoles = roles
				.Where(r => r != null)
				.Select(r => $"'{r}'")
				.Where(s => s is not null)
				.Where(s => s.Length > 2)
				.ToList();

			if (filteredRoles.Count == 0) {
				return string.Empty;
			}

			if (filteredRoles.Count == 1) {
				return filteredRoles[0];
			}

			if (filteredRoles.Count == 2) {
				return $"{filteredRoles[0]} or {filteredRoles[1]}";
			}

			// For 3 or more items, format as "a, b, or c"
			var allButLast = string.Join(", ", filteredRoles.Take(filteredRoles.Count - 1));
			return $"{allButLast}, or {filteredRoles.Last()}";
		}
	}

}