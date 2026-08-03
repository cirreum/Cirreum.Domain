namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Authorization;
using Cirreum.Conductor;
using Cirreum.Exceptions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The disabled-user gate. It denies only on an explicit <c>IsEnabled = false</c> from
/// whatever application-user record is present — owned or not — and it runs ahead of
/// every stage, so a disabled caller is denied on non-grantable operations too.
/// </summary>
/// <remarks>
/// The gate previously lived inside <c>OperationGrantEvaluator</c> and pattern-matched
/// <c>ApplicationUser is IOwnedApplicationUser { IsEnabled: true }</c>. That form failed
/// in both directions: a null record (operator/machine track, where disablement is the
/// identity provider's to enforce) was read as disabled, and a disabled record that did
/// not implement <c>IOwnedApplicationUser</c> was invisible to it.
/// </remarks>
public class ApplicationUserEnabledTests {

	private const string Disabled = "User is disabled.";

	[Fact]
	public async Task Caller_with_no_application_user_record_reaches_grant_resolution() {

		// The LapCast regression: the accessor marks resolution as attempted and stores
		// null for callers whose scheme has no registered resolver (operator/workforce
		// track). Loaded-with-null must pass the gate, not deny as USER_DISABLED.
		var user = TestUserState.CreateAuthenticated(
			TestGrantProvider.GrantedUserId, "Workforce Admin", [ApplicationRoles.AppUserRole],
			applicationUser: null,
			applicationUserResolved: true);
		using var provider = AuthorizationTestHost.Build(user);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var operation = new WriteWidgetCommand();
		var result = await dispatcher.DispatchAsync(operation);

		result.IsSuccess.Should().BeTrue(
			"absence of an application-user record is not disablement — dispatch failed with: {0}",
			result.Error?.ToString() ?? "<no error>");
		operation.OwnerId.Should().Be(TestGrantProvider.GrantedOwnerId);
	}

	[Fact]
	public async Task Disabled_owned_application_user_is_denied() {

		var user = TestUserState.CreateAuthenticated(
			TestGrantProvider.GrantedUserId, "Disabled Member", [ApplicationRoles.AppUserRole],
			applicationUser: new TestOwnedApplicationUser(OwnerId: "company-1") { IsEnabled = false });
		using var provider = AuthorizationTestHost.Build(user);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var operation = new WriteWidgetCommand();
		var result = await dispatcher.DispatchAsync(operation);

		result.IsSuccess.Should().BeFalse();
		result.Error.Should().BeOfType<ForbiddenAccessException>()
			.Which.Message.Should().Be(Disabled);
		operation.OwnerId.Should().BeNull("a disabled caller must be denied before any owner is stamped");
	}

	[Fact]
	public async Task Disabled_application_user_that_is_not_owned_is_denied() {

		// IsEnabled is declared on IApplicationUser, not IOwnedApplicationUser. An app whose
		// user type has no owner dimension still gets its disablement honoured.
		var user = TestUserState.CreateAuthenticated(
			TestGrantProvider.GrantedUserId, "Disabled User", [ApplicationRoles.AppUserRole],
			applicationUser: new TestApplicationUser { IsEnabled = false });
		using var provider = AuthorizationTestHost.Build(user);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new WriteWidgetCommand());

		result.IsSuccess.Should().BeFalse();
		result.Error.Should().BeOfType<ForbiddenAccessException>()
			.Which.Message.Should().Be(Disabled);
	}

	[Fact]
	public async Task Enabled_application_user_that_is_not_owned_passes_the_gate() {

		var user = TestUserState.CreateAuthenticated(
			TestGrantProvider.GrantedUserId, "Active User", [ApplicationRoles.AppUserRole],
			applicationUser: new TestApplicationUser());
		using var provider = AuthorizationTestHost.Build(user);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var operation = new WriteWidgetCommand();
		var result = await dispatcher.DispatchAsync(operation);

		result.IsSuccess.Should().BeTrue(
			"ownership is orthogonal to enablement — dispatch failed with: {0}",
			result.Error?.ToString() ?? "<no error>");
		operation.OwnerId.Should().Be(TestGrantProvider.GrantedOwnerId);
	}

	[Fact]
	public async Task Disabled_application_user_is_denied_on_a_non_grantable_operation() {

		// The coverage gap the gate's old placement left open: while it lived inside the
		// grant evaluator it ran only for grantable operations, so a disabled caller kept
		// full access to every role-gated operation in the app. The role here is the one
		// the operation requires, so the only thing that can deny is the disabled gate.
		var user = TestUserState.CreateAuthenticated(
			"admin-123", "Disabled Admin", [ApplicationRoles.AppAdminRole],
			applicationUser: new TestOwnedApplicationUser(OwnerId: "company-1") { IsEnabled = false });
		using var provider = AuthorizationTestHost.Build(user);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new AdminOnlyCommand());

		result.IsSuccess.Should().BeFalse();
		result.Error.Should().BeOfType<ForbiddenAccessException>()
			.Which.Message.Should().Be(Disabled);
	}

	[Fact]
	public async Task Disabled_caller_is_reported_as_disabled_rather_than_as_having_no_roles() {

		// Apps commonly strip roles as part of disabling a user. The disabled fact is the
		// specific and actionable one, so the gate runs ahead of the no-roles check —
		// otherwise the denial reads "has no assigned roles" and sends the operator
		// looking in the wrong place.
		var user = TestUserState.CreateAuthenticated(
			TestGrantProvider.GrantedUserId, "Stripped User", [],
			applicationUser: new TestOwnedApplicationUser(OwnerId: "company-1") { IsEnabled = false });
		using var provider = AuthorizationTestHost.Build(user);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new WriteWidgetCommand());

		result.IsSuccess.Should().BeFalse();
		result.Error.Should().BeOfType<ForbiddenAccessException>()
			.Which.Message.Should().Be(Disabled);
	}

}
