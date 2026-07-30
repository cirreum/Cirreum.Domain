namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Authorization;
using Cirreum.Authorization.Operations.Grants;

/// <summary>
/// Grant resolver keyed by caller id — deterministic across parallel tests, no shared
/// mutable state. <see cref="GrantedUserId"/> holds a single grant on
/// <see cref="GrantedOwnerId"/>; every other caller has no grants.
/// </summary>
internal sealed class TestGrantProvider : IOperationGrantProvider {

	public const string GrantedUserId = "granted-user";
	public const string GrantedOwnerId = "company-1";

	public ValueTask<OperationGrantResult> ResolveGrantsAsync<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> context,
		CancellationToken cancellationToken)
		where TAuthorizableObject : IAuthorizableObject {

		string[] owners = context.UserState.Id == GrantedUserId
			? [GrantedOwnerId]
			: [];
		return new(new OperationGrantResult(owners));
	}

}
