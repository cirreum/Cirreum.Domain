namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Authorization;
using Cirreum.Authorization.Operations.Grants;

/// <summary>
/// Grant resolver whose answer a test can revoke mid-run. Generic so assembly scanning
/// (which skips generic type definitions) never selects it in place of
/// <see cref="TestGrantProvider"/>; close it over the test class that registers it.
/// </summary>
internal sealed class RevocableGrantProvider<TOwner> : IOperationGrantProvider {

	public bool IsGranted { get; set; } = true;

	public ValueTask<OperationGrantResult> ResolveGrantsAsync<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> context,
		CancellationToken cancellationToken)
		where TAuthorizableObject : IAuthorizableObject {

		string[] owners = this.IsGranted ? [TestGrantProvider.GrantedOwnerId] : [];
		return new(new OperationGrantResult(owners));
	}

}
