namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Authorization;
using Cirreum.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Composes the REAL opinionated domain pipeline — <c>AddGrantAuthorization</c> +
/// <c>AddDomainServices</c>, mirroring the host runtimes' composition — with a fixed
/// test user. The regression under test is registration, so tests must go through the
/// production registration path, not a hand-rolled conductor.
/// </summary>
internal static class AuthorizationTestHost {

	/// <param name="user">The fixed caller every dispatch sees.</param>
	/// <param name="configure">Registrations applied before the domain pipeline, which skips what is already registered.</param>
	public static ServiceProvider Build(TestUserState user, Action<IServiceCollection>? configure = null) {

		var services = new ServiceCollection();
		var configuration = new ConfigurationBuilder().Build();

		configure?.Invoke(services);
		services.AddLogging();
		services.AddSingleton<IUserStateAccessor>(new FixedUserStateAccessor(user));
		services.AddSingleton<IAuthorizationRoleRegistry>(sp => {
			var registry = new TestRoleRegistry(sp.GetRequiredService<ILogger<TestRoleRegistry>>());
			registry.Initialize();
			return registry;
		});

		services.AddGrantAuthorization(configuration);
		services.AddDomainServices(configuration);

		return services.BuildServiceProvider();
	}

}
