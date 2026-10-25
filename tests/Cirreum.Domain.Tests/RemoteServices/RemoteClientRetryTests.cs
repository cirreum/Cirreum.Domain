namespace Cirreum.Domain.Tests.RemoteServices;

using System.Diagnostics;
using System.Net;

public class RemoteClientRetryTests {

	private static TestRemoteClient CreateClient() =>
		TestRemoteClient.Create(StubHttpHandler.Returning(HttpStatusCode.OK));

	[Fact]
	public async Task TransientFailureOnEveryAttempt_ReturnsTheLastFailedResult() {
		var calls = 0;

		var result = await CreateClient().Retry(_ => {
			calls++;
			return Task.FromResult(Result<string>.Fail(new HttpRequestException("unreachable")));
		}, maxAttempts: 2);

		result.IsFailure.Should().BeTrue();
		result.Error.Should().BeOfType<HttpRequestException>();
		calls.Should().Be(2);
	}

	[Fact]
	public async Task NonTransientFailure_IsReturnedWithoutRetrying() {
		var calls = 0;
		var stopwatch = Stopwatch.StartNew();

		var result = await CreateClient().Retry(_ => {
			calls++;
			return Task.FromResult(Result<string>.Fail(new InvalidOperationException("not found")));
		}, maxAttempts: 3);

		result.IsFailure.Should().BeTrue();
		result.Error.Should().BeOfType<InvalidOperationException>();
		calls.Should().Be(1);
		stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public async Task TransientFailureThenSuccess_ReturnsTheSuccess() {
		var calls = 0;

		var result = await CreateClient().Retry(_ => {
			calls++;
			return Task.FromResult(calls == 1
				? Result<string>.Fail(new HttpRequestException("blip"))
				: Result<string>.Success("ok"));
		}, maxAttempts: 3);

		result.IsSuccess.Should().BeTrue();
		result.Value.Should().Be("ok");
		calls.Should().Be(2);
	}

	[Fact]
	public async Task TransientExceptionOnTheFinalAttempt_Propagates() {
		var calls = 0;

		var act = () => CreateClient().Retry<string>(_ => {
			calls++;
			throw new HttpRequestException("down");
		}, maxAttempts: 2);

		await act.Should().ThrowAsync<HttpRequestException>();
		calls.Should().Be(2);
	}

	[Fact]
	public async Task NonGeneric_TransientFailureOnEveryAttempt_ReturnsTheLastFailedResult() {
		var calls = 0;

		var result = await CreateClient().Retry(_ => {
			calls++;
			return Task.FromResult(Result.Fail(new HttpRequestException("unreachable")));
		}, maxAttempts: 2);

		result.IsFailure.Should().BeTrue();
		calls.Should().Be(2);
	}

	[Fact]
	public async Task ZeroAttempts_IsRejected() {
		var act = () => CreateClient().Retry(_ => Task.FromResult(Result.Success), maxAttempts: 0);

		await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
	}

}
