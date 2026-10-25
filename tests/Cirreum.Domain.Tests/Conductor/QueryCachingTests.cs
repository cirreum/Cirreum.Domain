namespace Cirreum.Domain.Tests.Conductor;

using Cirreum.Caching;
using Cirreum.Caching.Configuration;
using Cirreum.Conductor;
using Cirreum.Conductor.Configuration;
using Cirreum.Conductor.Intercepts;
using Cirreum.Domain.Tests.AuthorizationPipeline;
using Microsoft.Extensions.Logging.Abstractions;

public class QueryCachingTests {

	public sealed record CachedOrdersQuery(string Region) : ICacheableOperation<string> {
		public string CacheKey => $"orders:{this.Region}";
		public CacheExpirationPolicy CacheExpiration => new(Expiration: TimeSpan.FromMinutes(1));
		public string[]? CacheTags => ["orders"];
	}

	/// <summary>Runs the factory on every call and records what the intercept passed in.</summary>
	private sealed class RecordingCacheService : ICacheService {

		public string? Key { get; private set; }
		public string[]? Tags { get; private set; }
		public CacheExpirationPolicy? Settings { get; private set; }

		public ValueTask<TResultValue> GetOrCreateAsync<TResultValue>(
			string cacheKey,
			Func<CancellationToken, ValueTask<TResultValue>> factory,
			CacheExpirationPolicy settings,
			string[]? tags = null,
			CancellationToken cancellationToken = default) {
			this.Key = cacheKey;
			this.Tags = tags;
			this.Settings = settings;
			return factory(cancellationToken);
		}

		public ValueTask RemoveAsync(string cacheKey, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
		public ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
		public ValueTask RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

	}

	private static async Task<(RecordingCacheService Cache, Result<string> Result)> RunAsync(
		ConductorSettings? conductorSettings = null,
		CacheSettings? cacheSettings = null,
		CacheKeyContext? keyContext = null,
		Result<string>? handlerResult = null) {

		var cache = new RecordingCacheService();
		var intercept = new QueryCaching<CachedOrdersQuery, string>(
			cache,
			conductorSettings ?? new ConductorSettings(),
			cacheSettings ?? new CacheSettings(),
			keyContext ?? new CacheKeyContext(),
			NullLogger<QueryCaching<CachedOrdersQuery, string>>.Instance);

		var context = OperationContext<CachedOrdersQuery>.Create(
			TestUserState.CreateAuthenticated("user-1", "User", []),
			new CachedOrdersQuery("west"),
			nameof(CachedOrdersQuery),
			"op-1",
			"corr-1",
			startTimestamp: 0);

		var result = await intercept.HandleAsync(
			context,
			(_, _) => Task.FromResult(handlerResult ?? Result<string>.Success("orders")),
			CancellationToken.None);

		return (cache, result);
	}

	[Fact]
	public async Task Expiration_QueryValue_ThenOverride_ThenDefaults() {
		var conductorSettings = new ConductorSettings();
		conductorSettings.Cache.QueryOverrides[nameof(CachedOrdersQuery)] = new CacheExpirationOverride {
			LocalExpiration = TimeSpan.FromMinutes(2)
		};
		var cacheSettings = new CacheSettings {
			DefaultExpiration = new CacheExpirationOverride {
				Expiration = TimeSpan.FromMinutes(10),
				LocalExpiration = TimeSpan.FromMinutes(20),
				FailureExpiration = TimeSpan.FromMinutes(3)
			}
		};

		var (cache, _) = await RunAsync(conductorSettings, cacheSettings);

		cache.Settings.Should().Be(new CacheExpirationPolicy(
			Expiration: TimeSpan.FromMinutes(1),
			LocalExpiration: TimeSpan.FromMinutes(2),
			FailureExpiration: TimeSpan.FromMinutes(3)));
	}

	[Fact]
	public async Task Expiration_OverrideBeatsTheQueryValue() {
		var conductorSettings = new ConductorSettings();
		conductorSettings.Cache.QueryOverrides[nameof(CachedOrdersQuery)] = new CacheExpirationOverride {
			Expiration = TimeSpan.FromMinutes(5)
		};

		var (cache, _) = await RunAsync(conductorSettings);

		cache.Settings!.Expiration.Should().Be(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public async Task KeyAndTags_UseTheQueryValues_WhenNoContextIsSet() {
		var (cache, _) = await RunAsync();

		cache.Key.Should().Be("orders:west");
		cache.Tags.Should().Equal("orders");
	}

	[Fact]
	public async Task KeyAndTags_IncludeTheContextPrefixAndExtraTags() {
		var keyContext = new CacheKeyContext();
		keyContext.SetPrefix("owner:company-1");
		keyContext.SetExtraTags(["owner:company-1"]);

		var (cache, _) = await RunAsync(keyContext: keyContext);

		cache.Key.Should().Be("owner:company-1:orders:west");
		cache.Tags.Should().Equal("owner:company-1", "orders");
	}

	[Fact]
	public async Task FailedHandlerResult_PassesThroughTheCache() {
		var failure = new InvalidOperationException("store offline");

		var (_, result) = await RunAsync(handlerResult: Result<string>.Fail(failure));

		result.IsFailure.Should().BeTrue();
		result.Error.Should().BeSameAs(failure);
	}

}
