namespace Cirreum.Caching;

using System.Collections.Concurrent;

/// <summary>
/// Single-instance in-memory <see cref="ICacheService"/> implementation. Supports absolute expiration and
/// failure-aware expiration. Suitable for Blazor WASM, development, testing, and single-instance deployments.
/// </summary>
/// <remarks>
/// Cache-aside reads are <b>not</b> single-flight: concurrent misses for the same key may each invoke the
/// factory (last write wins) — matching the <c>IDistributedCache</c> provider; use the
/// <c>HybridCache</c>-backed provider when stampede coalescing is required. Expired entries are never
/// returned, and are evicted opportunistically by a single-sweeper pass (triggered on cache misses) so
/// high-cardinality keys that are never re-requested cannot accumulate.
/// </remarks>
public class InMemoryCacheService : ICacheService {
	private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

	// Opportunistic eviction of expired entries: high-cardinality keys that are never re-requested would
	// otherwise linger forever. A single-sweeper pass every Nth miss keeps the cost amortized and bounded.
	private const int SweepThreshold = 1000;
	private int _opsSinceSweep;
	private int _sweeping; // 0 = idle, 1 = a sweep is in progress (single-sweeper gate).

	public async ValueTask<TResultValue> GetOrCreateAsync<TResultValue>(
		string cacheKey,
		Func<CancellationToken, ValueTask<TResultValue>> factory,
		CacheExpirationPolicy settings,
		string[]? tags = null,
		CancellationToken cancellationToken = default) {

		// Check if exists and not expired
		if (this._cache.TryGetValue(cacheKey, out var existing) && !existing.IsExpired) {
			return (TResultValue)existing.Value;
		}

		// Miss (absent or expired): opportunistically sweep expired entries, then (re)compute and store.
		this.MaybeSweep();

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

	public ValueTask RemoveByTagsAsync(IEnumerable<string> tags, CancellationToken cancellationToken = default) {
		ArgumentNullException.ThrowIfNull(tags);

		var tagSet = tags.ToHashSet(StringComparer.Ordinal);
		if (tagSet.Count == 0) {
			return ValueTask.CompletedTask;
		}

		// One pass over the cache instead of a full scan per tag; TryRemove(pair) only evicts an unchanged entry.
		foreach (var pair in this._cache) {
			if (pair.Value.Tags is { } entryTags && entryTags.Any(tagSet.Contains)) {
				this._cache.TryRemove(pair);
			}
		}

		return ValueTask.CompletedTask;
	}

	private void MaybeSweep() {
		if (Interlocked.Increment(ref this._opsSinceSweep) < SweepThreshold) {
			return;
		}

		// Single-sweeper: only the thread that flips _sweeping 0->1 performs the O(N) scan; concurrent
		// threads that also crossed the threshold skip it. Resetting the op counter inside the claim keeps
		// it bounded, so it cannot run away toward overflow.
		if (Interlocked.CompareExchange(ref this._sweeping, 1, 0) != 0) {
			return;
		}

		try {
			Interlocked.Exchange(ref this._opsSinceSweep, 0);

			foreach (var pair in this._cache) {
				if (pair.Value.IsExpired) {
					// KeyValuePair overload removes only if the entry is unchanged — never evicts a fresh write.
					this._cache.TryRemove(pair);
				}
			}
		} finally {
			Interlocked.Exchange(ref this._sweeping, 0);
		}
	}

	private static DateTime? CalculateExpiration<TResultValue>(TResultValue value, CacheExpirationPolicy settings) {
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
