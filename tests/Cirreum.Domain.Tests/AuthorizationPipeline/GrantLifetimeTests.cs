namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Authorization;
using Cirreum.Authorization.Operations.Grants;
using Cirreum.Conductor;
using Cirreum.Exceptions;
using Microsoft.Extensions.DependencyInjection;

public class GrantLifetimeTests {

	[Fact]
	public async Task SingletonResolver_RevokedGrant_IsDeniedOnTheNextRequest() {
		var user = TestUserState.CreateAuthenticated(
			TestGrantProvider.GrantedUserId, "Granted User", [ApplicationRoles.AppUserRole]);
		using var provider = AuthorizationTestHost.Build(user, services =>
			services.AddOperationGrants<RevocableGrantProvider<GrantLifetimeTests>>(ServiceLifetime.Singleton));
		var resolver = (RevocableGrantProvider<GrantLifetimeTests>)provider.GetRequiredService<IOperationGrantProvider>();

		using (var firstRequest = provider.CreateScope()) {
			var granted = await firstRequest.ServiceProvider.GetRequiredService<IDispatcher>()
				.DispatchAsync(new WriteWidgetCommand());
			granted.IsSuccess.Should().BeTrue();
		}

		resolver.IsGranted = false;

		using var secondRequest = provider.CreateScope();
		var revoked = await secondRequest.ServiceProvider.GetRequiredService<IDispatcher>()
			.DispatchAsync(new WriteWidgetCommand());

		revoked.IsSuccess.Should().BeFalse();
		revoked.Error.Should().BeOfType<ForbiddenAccessException>();
	}

	[Theory]
	[InlineData(ServiceLifetime.Singleton)]
	[InlineData(ServiceLifetime.Scoped)]
	[InlineData(ServiceLifetime.Transient)]
	public void Factory_IsScoped_WhateverTheResolverLifetime(ServiceLifetime resolverLifetime) {
		var services = new ServiceCollection();

		services.AddOperationGrants<RevocableGrantProvider<GrantLifetimeTests>>(resolverLifetime);

		services.Single(d => d.ServiceType == typeof(IOperationGrantProvider)).Lifetime.Should().Be(resolverLifetime);
		services.Single(d => d.ServiceType == typeof(IOperationGrantFactory)).Lifetime.Should().Be(ServiceLifetime.Scoped);
	}

}
