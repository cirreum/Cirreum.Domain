namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Authorization;
using Cirreum.Conductor;
using Cirreum.Exceptions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dispatch-level enforcement tests — the regression this suite exists for. Each test
/// dispatches through the full production pipeline (AddDomainServices composition) and
/// asserts that operation-level authorization actually RUNS, not merely that it is
/// registered.
/// </summary>
public class AuthorizationDispatchTests {

	[Fact]
	public async Task Non_admin_dispatching_a_role_gated_operation_is_denied() {

		var user = TestUserState.CreateAuthenticated(
			"user-123", "Regular User", [ApplicationRoles.AppUserRole]);
		using var provider = AuthorizationTestHost.Build(user);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new AdminOnlyCommand());

		result.IsSuccess.Should().BeFalse();
		result.Error.Should().BeOfType<ForbiddenAccessException>();
	}

	[Fact]
	public async Task Admin_dispatching_a_role_gated_operation_succeeds() {

		var user = TestUserState.CreateAuthenticated(
			"admin-123", "Admin User", [ApplicationRoles.AppAdminRole]);
		using var provider = AuthorizationTestHost.Build(user);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new AdminOnlyCommand());

		result.IsSuccess.Should().BeTrue("dispatch failed with: {0}", result.Error?.ToString() ?? "<no error>");
		result.Value.Should().Be("admin-data");
	}

	[Fact]
	public async Task Owner_mutate_with_null_OwnerId_and_a_single_grant_is_auto_stamped() {

		var user = TestUserState.CreateAuthenticated(
			TestGrantProvider.GrantedUserId, "Granted User", [ApplicationRoles.AppUserRole]);
		using var provider = AuthorizationTestHost.Build(user);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var operation = new WriteWidgetCommand();
		var result = await dispatcher.DispatchAsync(operation);

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().Be(TestGrantProvider.GrantedOwnerId);
		operation.OwnerId.Should().Be(TestGrantProvider.GrantedOwnerId);
	}

	[Fact]
	public async Task Owner_mutate_with_zero_grants_is_denied_before_the_handler() {

		var user = TestUserState.CreateAuthenticated(
			"ungranted-user", "Ungranted User", [ApplicationRoles.AppUserRole]);
		using var provider = AuthorizationTestHost.Build(user);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var operation = new WriteWidgetCommand();
		var result = await dispatcher.DispatchAsync(operation);

		result.IsSuccess.Should().BeFalse();
		result.Error.Should().BeOfType<ForbiddenAccessException>();
		operation.OwnerId.Should().BeNull("the grant gate must not stamp an owner the caller was never granted");
	}

	[Fact]
	public async Task Home_company_membership_without_a_grant_record_is_denied() {

		// Home membership is a grant record, never inferred from identity: a caller whose
		// ApplicationUser carries a home OwnerId but who has zero grant records must be
		// denied. Fails loud if an implicit home-owner merge ever creeps back into the
		// grant factory.
		var user = TestUserState.CreateAuthenticated(
			"home-user", "Home User", [ApplicationRoles.AppUserRole],
			applicationUser: new TestOwnedApplicationUser(OwnerId: "home-company"));
		using var provider = AuthorizationTestHost.Build(user);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var operation = new WriteWidgetCommand();
		var result = await dispatcher.DispatchAsync(operation);

		result.IsSuccess.Should().BeFalse();
		result.Error.Should().BeOfType<ForbiddenAccessException>();
		operation.OwnerId.Should().BeNull("identity alone must never stamp the caller's home owner");
	}

	[Fact]
	public async Task Operation_with_no_authorization_source_is_denied_fail_closed() {

		var user = TestUserState.CreateAuthenticated(
			"admin-123", "Admin User", [ApplicationRoles.AppAdminRole]);
		using var provider = AuthorizationTestHost.Build(user);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new NakedOperation());

		result.IsSuccess.Should().BeFalse();
		result.Error.Should().BeOfType<InvalidOperationException>()
			.Which.Message.Should().Contain("no authorizers");
	}

	[Fact]
	public async Task Unauthenticated_caller_dispatching_an_authorizable_operation_is_denied() {

		using var provider = AuthorizationTestHost.Build(TestUserState.CreateUnauthenticated());
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new AdminOnlyCommand());

		result.IsSuccess.Should().BeFalse();
		result.Error.Should().BeOfType<UnauthenticatedAccessException>();
	}

}
