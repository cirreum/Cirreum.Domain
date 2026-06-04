namespace Cirreum.Caching;

using System.Collections.Concurrent;

/// <summary>
/// Single-instance in-memory <see cref="ICacheService"/> implementation. Supports
/// absolute expiration via <see cref="CacheExpirationSettings.Expiration"/> and failure-aware
/// expiration. Suitable for Blazor WASM, development, testing, and single-instance deployments.
/// </summary>
public class InMemoryCacheService : ICacheService {
	private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

	public async ValueTask<TResultValue> GetOrCreateAsync<TResultValue>(
		string cacheKey,
		Func<CancellationToken, ValueTask<TResultValue>> factory,
		CacheExpirationSettings settings,
		string[]? tags = null,
		CancellationToken cancellationToken = default) {

		// Check if exists and not expired
		if (this._cache.TryGetValue(cacheKey, out var existing) && !existing.IsExpired) {
			return (TResultValue)existing.Value;
		}

		// Create new entry
		var value = await factory(cancellationToken);
		var expiration = CalculateExpiration(value, settings);

		var entry = new CacheEntry(value!, expiration, tags);
		this._cache[cacheKey] = entry;

		return value;
	}

	public ValueTask RemoveAsync(string cacheKey, CancellationToken cancellationToken = default) {
		this._cache.TryRemove(cacheKey, out _);
		return ValueTask.CompletedTask;
	}

	public ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) {
		var keysToRemove = this._cache
			.Where(kvp => kvp.Value.Tags?.Contains(tag) == true)
			.Select(kvp => kvp.Key)
			.ToList();

		foreach (var key in keysToRemove) {
			this._cache.TryRemove(key, out _);
		}

		return ValueTask.CompletedTask;
	}

	public async ValueTask RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default) {
		foreach (var tag in tags) {
			await this.RemoveByTagAsync(tag, cancellationToken);
		}
	}

	private static DateTime? CalculateExpiration<TResultValue>(TResultValue value, CacheExpirationSettings settings) {
		// Check if it's a failed result
		if (value is IResult { IsSuccess: false } && settings.FailureExpiration.HasValue) {
			return DateTime.UtcNow.Add(settings.FailureExpiration.Value);
		}

		// Use standard expiration (ignoring LocalExpiration for in-memory)
		if (settings.Expiration.HasValue) {
			return DateTime.UtcNow.Add(settings.Expiration.Value);
		}

		return null; // No expiration
	}

	private sealed class CacheEntry(object value, DateTime? expiresAt, string[]? tags) {

		public object Value { get; } = value;

		public DateTime? ExpiresAt { get; } = expiresAt;

		public string[]? Tags { get; } = tags;

		public bool IsExpired => this.ExpiresAt.HasValue && DateTime.UtcNow >= this.ExpiresAt.Value;

	}

}