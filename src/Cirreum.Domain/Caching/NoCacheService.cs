namespace Cirreum.Caching;

/// <summary>
/// No-op <see cref="ICacheService"/> that always invokes the factory directly,
/// bypassing any caching. Registered automatically when <see cref="CacheSettings.Provider"/>
/// is <see cref="CacheProvider.None"/>.
/// </summary>
public class NoCacheService : ICacheService {
	public async ValueTask<TResultValue> GetOrCreateAsync<TResultValue>(
		string cacheKey,
		Func<CancellationToken, ValueTask<TResultValue>> factory,
		CacheExpirationSettings settings,
		string[]? tags = null,
		CancellationToken cancellationToken = default) {
		// Always execute, never cache
		return await factory(cancellationToken);
	}

	public ValueTask RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
		=> ValueTask.CompletedTask;

	public ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
		=> ValueTask.CompletedTask;

	public ValueTask RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default)
		=> ValueTask.CompletedTask;
}