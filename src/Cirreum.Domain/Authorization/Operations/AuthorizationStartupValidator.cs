namespace Cirreum.Authorization.Operations;

using Cirreum.Authorization.Operations.Grants;
using Cirreum.Extensions;

/// <summary>
/// Boot-time validation for the operation-authorization pipeline. Detects authorizable
/// operations that can never pass authorization — operations the evaluator will deny on
/// every dispatch because no authorization source exists that could produce a pass.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the runtime deny condition in <see cref="DefaultAuthorizationEvaluator"/>: an
/// operation with no <see cref="IAuthorizer{TAuthorizableObject}"/>, no grant detection
/// surface, and no <see cref="IAuthorizationConstraint"/> or <see cref="IPolicyAuthorizer"/>
/// anywhere in the application is denied on every dispatch. Surfacing that at boot turns a
/// silent always-deny operation into an immediate, actionable failure.
/// </para>
/// <para>
/// The check is intentionally conservative: when any constraint or policy authorizer type
/// exists, no operation is provably dead (constraints and policies are evaluated app-wide
/// at dispatch time), so the validation stays silent rather than risk failing a valid
/// composition.
/// </para>
/// </remarks>
internal static class AuthorizationStartupValidator {

	/// <summary>
	/// Returns the concrete <see cref="IAuthorizableOperationBase"/> types that will be
	/// denied on every dispatch, given the available types from the scanned assemblies.
	/// Empty when nothing is provably dead.
	/// </summary>
	/// <param name="availableTypes">The distinct types discovered in the scanned assemblies.</param>
	internal static List<Type> FindUnauthorizableOperations(IEnumerable<Type> availableTypes) {

		var operationTypes = new List<Type>();
		var authorizedTargets = new HashSet<Type>();
		var hasConstraintsOrPolicies = false;

		foreach (var type in availableTypes) {
			if (!type.IsConcreteClass()) {
				continue;
			}

			if (typeof(IAuthorizableOperationBase).IsAssignableFrom(type)) {
				operationTypes.Add(type);
			}

			if (typeof(IAuthorizationConstraint).IsAssignableFrom(type)
				|| typeof(IPolicyAuthorizer).IsAssignableFrom(type)) {
				hasConstraintsOrPolicies = true;
			}

			var authorizerInterface = type.GetFirstMatchingGenericInterface(typeof(IAuthorizer<>));
			if (authorizerInterface is not null) {
				authorizedTargets.Add(authorizerInterface.GenericTypeArguments[0]);
			}
		}

		if (operationTypes.Count == 0 || hasConstraintsOrPolicies) {
			return [];
		}

		return [.. operationTypes.Where(op => !IsGrantable(op) && !authorizedTargets.Contains(op))];
	}

	private static bool IsGrantable(Type operationType) =>
		typeof(IGrantableMutateBase).IsAssignableFrom(operationType)
		|| typeof(IGrantableLookupBase).IsAssignableFrom(operationType)
		|| typeof(IGrantableSearchBase).IsAssignableFrom(operationType)
		|| typeof(IGrantableSelfBase).IsAssignableFrom(operationType);

}
