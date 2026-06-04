namespace Cirreum.Caching;

/// <summary>
/// Decorator that wraps any <see cref="ICacheService"/> implementation to provide
/// standardized cache telemetry (hit/miss counters and operation duration histograms)
/// via <see cref="CacheTelemetry"/>.
/// </summary>
/// <remarks>
/// <para>
/// Hit/miss detection uses a captured <c>factoryExecuted</c> boolean — the same
/// pattern used by <c>HybridCacheableQueryService</c>. When the inner cache serves
/// a cached value, the factory is never invoked and <c>factoryExecuted</c> stays
/// <see langword="false"/> (hit). When the factory runs, it's a miss.
/// </para>
/// <para>
/// Registered automatically by <see cref="CacheServiceCollectionExtensions"/> when
/// the configured provider is not <see cref="CacheProvider.None"/>. Wrapping
/// <see cref="NoCacheService"/> is skipped — there's no cache to observe.
/// </para>
/// </remarks>
/// <param name="inner">The underlying cache service to decorate.</param>
/// <param name="consumer">
/// Subsystem identifier baked into every metric emitted by this instance
/// (e.g. "query-caching", "grant-resolution", "other").
/// </param>
sealed class InstrumentedCacheService(ICacheService inner, string consumer) : ICacheService {

	/// <summary>
	/// The underlying cache service being decorated. Exposed so DI registration
	/// can unwrap the decorator when building keyed instances (avoids double-instrumentation).
	/// </summary>
	internal ICacheService Inner => inner;

	public async ValueTask<TResultValue> GetOrCreateAsync<TResultValue>(
		string cacheKey,
		Func<CancellationToken, ValueTask<TResultValue>> factory,
		CacheExpirationSettings settings,
		string[]? tags = null,
		CancellationToken cancellationToken = default) {

		var startTimestamp = Timing.Start();
		var factoryExecuted = false;

		var result = await inner.GetOrCreateAsync(
			cacheKey,
			async ct => {
				factoryExecuted = true;
				return await factory(ct);
			},
			settings,
			tags,
			cancellationToken);

		CacheTelemetry.RecordOperation(
			cacheKey,
			isHit: !factoryExecuted,
			Timing.GetElapsedMilliseconds(startTimestamp),
			consumer);

		return result;
	}

	public ValueTask RemoveAsync(
		string cacheKey,
		CancellationToken cancellationToken = default) {
		return inner.RemoveAsync(cacheKey, cancellationToken);
	}

	public ValueTask RemoveByTagAsync(
		string tag,
		CancellationToken cancellationToken = default) {
		return inner.RemoveByTagAsync(tag, cancellationToken);
	}

	public ValueTask RemoveByTagsAsync(
		IEnumerable<string> tags,
		CancellationToken cancellationToken = default) {
		return inner.RemoveByTagsAsync(tags, cancellationToken);
	}
}
