namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Authorization;
using Microsoft.Extensions.Logging;

internal sealed class TestRoleRegistry(
	ILogger<TestRoleRegistry> logger
) : AuthorizationRoleRegistryBase(logger) {

	public void Initialize() {
		this.DefaultInitializationAsync().GetAwaiter().GetResult();
	}

}
