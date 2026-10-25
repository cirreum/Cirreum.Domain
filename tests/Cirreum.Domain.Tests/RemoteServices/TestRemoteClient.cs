namespace Cirreum.Domain.Tests.RemoteServices;

using Cirreum.RemoteServices;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Test double exposing <see cref="RemoteClient"/>'s protected verbs and retry helpers.</summary>
internal sealed class TestRemoteClient(HttpClient client)
	: RemoteClient(client, NullLogger.Instance, new TestDomainEnvironment(), activitySource: null) {

	public static TestRemoteClient Create(StubHttpHandler handler) =>
		new(new HttpClient(handler) { BaseAddress = new Uri("https://remote.test/") });

	public Task<Result> Get(string endpoint, CancellationToken cancellationToken = default) =>
		this.GetAsync(endpoint, cancellationToken);

	public Task<Result<T>> GetJson<T>(string endpoint) => this.GetAsync<T>(endpoint);

	public Task<Result<T>> Retry<T>(Func<CancellationToken, Task<Result<T>>> operation, int maxAttempts) =>
		this.WithRetryAsync(operation, maxAttempts);

	public Task<Result> Retry(Func<CancellationToken, Task<Result>> operation, int maxAttempts) =>
		this.WithRetryAsync(operation, maxAttempts);

	private sealed class TestDomainEnvironment : IDomainEnvironment {
		public string ApplicationName => "test";
		public string EnvironmentName => "Development";
		public DomainRuntimeType RuntimeType => DomainRuntimeType.WebApi;
	}

}
