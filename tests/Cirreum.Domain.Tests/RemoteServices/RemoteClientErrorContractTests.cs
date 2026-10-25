namespace Cirreum.Domain.Tests.RemoteServices;

using Cirreum.Exceptions;
using System.Net;

/// <summary>
/// Pins which outcomes a Result-returning <c>RemoteClient</c> verb returns as a failed
/// <see cref="Result"/> and which it throws: 401, 403, and cancellation throw; everything
/// else is returned.
/// </summary>
public class RemoteClientErrorContractTests {

	private const string ProblemJson = """{"title":"Boom","detail":"Server exploded","status":500}""";

	[Fact]
	public async Task Unauthorized_Throws() {
		var client = TestRemoteClient.Create(StubHttpHandler.Returning(HttpStatusCode.Unauthorized));

		var act = () => client.Get("api/orders");

		await act.Should().ThrowAsync<UnauthenticatedAccessException>();
	}

	[Fact]
	public async Task Forbidden_Throws() {
		var client = TestRemoteClient.Create(StubHttpHandler.Returning(HttpStatusCode.Forbidden));

		var act = () => client.Get("api/orders");

		await act.Should().ThrowAsync<ForbiddenAccessException>();
	}

	[Fact]
	public async Task Cancellation_Throws() {
		var client = TestRemoteClient.Create(StubHttpHandler.Returning(HttpStatusCode.OK));
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		var act = () => client.Get("api/orders", cts.Token);

		await act.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task ServerErrorWithProblemDetails_ReturnsApiException() {
		var client = TestRemoteClient.Create(
			StubHttpHandler.Returning(HttpStatusCode.InternalServerError, ProblemJson, "application/problem+json"));

		var result = await client.Get("api/orders");

		result.IsFailure.Should().BeTrue();
		result.Error.Should().BeOfType<ApiException>()
			.Which.Message.Should().Be("Server exploded");
	}

	[Fact]
	public async Task SuccessStatusWithProblemDetails_ReturnsApiException() {
		var client = TestRemoteClient.Create(
			StubHttpHandler.Returning(HttpStatusCode.OK, ProblemJson, "application/problem+json"));

		var result = await client.Get("api/orders");

		result.IsFailure.Should().BeTrue();
		result.Error.Should().BeOfType<ApiException>();
	}

	[Fact]
	public async Task NullJsonBody_ReturnsFailure() {
		var client = TestRemoteClient.Create(StubHttpHandler.Returning(HttpStatusCode.OK, "null"));

		var result = await client.GetJson<OrderDto>("api/orders/1");

		result.IsFailure.Should().BeTrue();
		result.Error.Should().BeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task JsonBody_ReturnsTheValue() {
		var client = TestRemoteClient.Create(StubHttpHandler.Returning(HttpStatusCode.OK, """{"id":7}"""));

		var result = await client.GetJson<OrderDto>("api/orders/7");

		result.IsSuccess.Should().BeTrue();
		result.Value!.Id.Should().Be(7);
	}

	[Fact]
	public async Task NetworkFailure_ReturnsHttpRequestException() {
		var client = TestRemoteClient.Create(new StubHttpHandler(_ => throw new HttpRequestException("unreachable")));

		var result = await client.Get("api/orders");

		result.IsFailure.Should().BeTrue();
		result.Error.Should().BeOfType<HttpRequestException>();
	}

	[Fact]
	public async Task RequestRejectedBeforeSending_ReturnsFailure() {
		// No BaseAddress: HttpClient rejects the relative URI before the handler runs.
		var handler = StubHttpHandler.Returning(HttpStatusCode.OK);
		var client = new TestRemoteClient(new HttpClient(handler));

		var result = await client.Get("api/orders");

		result.IsFailure.Should().BeTrue();
		result.Error.Should().BeOfType<InvalidOperationException>();
		handler.Calls.Should().Be(0);
	}

	private sealed record OrderDto(int Id);

}
