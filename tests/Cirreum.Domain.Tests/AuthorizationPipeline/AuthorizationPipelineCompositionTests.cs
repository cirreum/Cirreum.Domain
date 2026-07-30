namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Authorization;
using Cirreum.Authorization.Operations;
using Cirreum.Authorization.Operations.Grants;
using Cirreum.Conductor;
using Cirreum.Conductor.Intercepts;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Pins the Conductor default pipeline composition. The Authorization intercept was
/// silently dropped from the pipeline once (fail-open); these tests make any recurrence
/// a hard test failure.
/// </summary>
public class AuthorizationPipelineCompositionTests {

	private static readonly TestUserState User =
		TestUserState.CreateAuthenticated("user-123", "Test User", [ApplicationRoles.AppUserRole]);

	[Fact]
	public void Default_pipeline_registers_the_spine_intercepts_in_fixed_order() {

		// Registration order in the service collection = pipeline execution order.
		var openGenericIntercepts = GetOpenGenericInterceptImplementations();

		openGenericIntercepts.Should().ContainInOrder(
			typeof(Validation<,>),
			typeof(Authorization<,>),
			typeof(GrantedLookupAudit<,>),
			typeof(HandlerPerformance<,>),
			typeof(QueryCaching<,>));
	}

	private static List<Type> GetOpenGenericInterceptImplementations() {
		var services = new ServiceCollection();
		var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
		services.AddLogging();
		services.AddDomainServices(configuration);

		return [.. services
			.Where(sd => sd.ServiceType == typeof(IIntercept<,>))
			.Select(sd => sd.ImplementationType!)];
	}

	[Fact]
	public void Authorizable_operation_resolves_Authorization_between_Validation_and_HandlerPerformance() {

		using var provider = AuthorizationTestHost.Build(User);

		var intercepts = provider.GetServices<IIntercept<AdminOnlyCommand, string>>().ToList();

		intercepts.Should().HaveCount(3);
		intercepts[0].Should().BeOfType<Validation<AdminOnlyCommand, string>>();
		intercepts[1].Should().BeOfType<Authorization<AdminOnlyCommand, string>>();
		intercepts[2].Should().BeOfType<HandlerPerformance<AdminOnlyCommand, string>>();
	}

	[Fact]
	public void Grantable_lookup_additionally_resolves_the_GrantedLookupAudit_intercept() {

		using var provider = AuthorizationTestHost.Build(User);

		var intercepts = provider.GetServices<IIntercept<ReadWidgetQuery, string>>().ToList();

		intercepts.Should().HaveCount(4);
		intercepts[0].Should().BeOfType<Validation<ReadWidgetQuery, string>>();
		intercepts[1].Should().BeOfType<Authorization<ReadWidgetQuery, string>>();
		intercepts[2].Should().BeOfType<GrantedLookupAudit<ReadWidgetQuery, string>>();
		intercepts[3].Should().BeOfType<HandlerPerformance<ReadWidgetQuery, string>>();
	}

	[Fact]
	public void Non_authorizable_operation_does_not_resolve_the_Authorization_intercept() {

		using var provider = AuthorizationTestHost.Build(User);

		// NakedOperation IS authorizable; use an intercept query against a plain operation
		// shape via the composition helper types below.
		var intercepts = provider.GetServices<IIntercept<PlainOperation, string>>().ToList();

		intercepts.Should().HaveCount(2);
		intercepts[0].Should().BeOfType<Validation<PlainOperation, string>>();
		intercepts[1].Should().BeOfType<HandlerPerformance<PlainOperation, string>>();
	}

}

// Non-authorizable control operation for the composition tests.
public sealed record PlainOperation : IOperation<string>;

public sealed class PlainOperationHandler : IOperationHandler<PlainOperation, string> {
	public Task<Result<string>> HandleAsync(PlainOperation operation, CancellationToken cancellationToken)
		=> Task.FromResult(Result<string>.Success("plain"));
}
