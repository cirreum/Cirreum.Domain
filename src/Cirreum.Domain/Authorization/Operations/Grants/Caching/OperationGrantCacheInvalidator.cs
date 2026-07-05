namespace Cirreum.Authorization.Operations.Grants.Caching;

using Cirreum.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Default implementation of <see cref="IOperationGrantCacheInvalidator"/>. Delegates to the
/// application's registered <see cref="ICacheService"/> for tag-based removal.
/// Registered as a singleton.
/// </summary>
public sealed class OperationGrantCacheInvalidator(
	[FromKeyedServices(CacheConsumers.GrantResolution)] ICacheService cacheService
) : IOperationGrantCacheInvalidator {

	/// <inheritdoc />
	public ValueTask InvalidateCallerAsync(
		string callerId,
		CancellationToken cancellationToken = default) {

		ArgumentException.ThrowIfNullOrWhiteSpace(callerId);
		var tag = OperationGrantCacheKeys.CallerTag(callerId);
		return cacheService.RemoveByTagAsync(tag, cancellationToken);
	}

	/// <inheritdoc />
	public ValueTask InvalidateFeatureAsync(
		string feature,
		CancellationToken cancellationToken = default) {

		ArgumentException.ThrowIfNullOrWhiteSpace(feature);
		var tag = OperationGrantCacheKeys.FeatureTag(feature);
		return cacheService.RemoveByTagAsync(tag, cancellationToken);
	}
}
