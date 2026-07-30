namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Authorization.Operations;

public class AuthorizationStartupValidatorTests {

	[Fact]
	public void Flags_an_authorizable_operation_with_no_authorization_source() {

		var dead = AuthorizationStartupValidator.FindUnauthorizableOperations(
			[typeof(NakedOperation), typeof(NakedOperationHandler)]);

		dead.Should().ContainSingle().Which.Should().Be(typeof(NakedOperation));
	}

	[Fact]
	public void Does_not_flag_an_operation_with_a_matching_authorizer() {

		var dead = AuthorizationStartupValidator.FindUnauthorizableOperations(
			[typeof(AdminOnlyCommand), typeof(AdminOnlyCommandAuthorizer)]);

		dead.Should().BeEmpty();
	}

	[Fact]
	public void Does_not_flag_a_grantable_operation() {

		var dead = AuthorizationStartupValidator.FindUnauthorizableOperations(
			[typeof(WriteWidgetCommand), typeof(ReadWidgetQuery)]);

		dead.Should().BeEmpty();
	}

	[Fact]
	public void Returns_empty_when_no_authorizable_operations_exist() {

		var dead = AuthorizationStartupValidator.FindUnauthorizableOperations(
			[typeof(PlainOperation), typeof(PlainOperationHandler)]);

		dead.Should().BeEmpty();
	}

	[Fact]
	public void Flags_only_the_uncovered_operations_in_a_mixed_set() {

		var dead = AuthorizationStartupValidator.FindUnauthorizableOperations([
			typeof(NakedOperation),
			typeof(AdminOnlyCommand),
			typeof(AdminOnlyCommandAuthorizer),
			typeof(WriteWidgetCommand)]);

		dead.Should().ContainSingle().Which.Should().Be(typeof(NakedOperation));
	}

}
