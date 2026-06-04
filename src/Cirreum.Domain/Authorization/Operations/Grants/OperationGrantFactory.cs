namespace Cirreum.Authorization.Operations.Grants;

using Cirreum.Authorization.Operations.Grants.Caching;
using Cirreum.Caching;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Core's <see cref="IOperationGrantFactory"/> implementation. Composes an
/// app-provided <see cref="IOperationGrantProvider"/> and owns every piece of
/// grant-translation policy so apps never touch <see cref="OperationGrant"/> directly.
/// </summary>
/// <remarks>
/// <para>
/// Factory flow:
/// </para>
/// <list type="number">
///   <item><description>Unauthenticated caller → <see cref="OperationGrant.Denied"/>.</description></item>
///   <item><description><see cref="IOperationGrantProvider.ShouldBypassAsync"/> returns <see langword="true"/> → <see cref="OperationGrant.Unrestricted"/> (always live, never cached).</description></item>
///   <item><description>No <see cref="AuthorizationContext{TAuthorizableObject}.RequiredGrants"/> declared → <see cref="OperationGrant.Denied"/> (misconfig guard).</description></item>
///   <item><description>L1 check: scoped in-memory dictionary keyed by cache key string.</description></item>
///   <item><description>L2 check: <see cref="ICacheService"/> via <c>GetOrCreateAsync</c>.</description></item>
///   <item><description>Cold path: invoke <see cref="IOperationGrantProvider.ResolveGrantsAsync"/> + <see cref="IOperationGrantProvider.ResolveHomeOwnerAsync"/> + merge.</description></item>
/// </list>
/// </remarks>
sealed class OperationGrantFactory(
	IOperationGrantProvider grantResolver,
	[FromKeyedServices(CacheConsumers.GrantResolution)] ICacheService cacheService,
	CacheSettings rootCacheSettings,
	OperationGrantCacheSettings cacheSettings
) : IOperationGrantFactory {

	// L1: scoped memoization — same cache key string as L2 for shared identity
	private readonly Dictionary<string, OperationGrant> _grantCache = [];

	public async ValueTask<OperationGrant> CreateAsync<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> context,
		CancellationToken cancellationToken)
		where TAuthorizableObject : IAuthorizableObject {

		ArgumentNullException.ThrowIfNull(context);

		var resourceType = typeof(TAuthorizableObject).Name;

		if (!context.IsAuthenticated) {
			AuthorizationTelemetry.RecordGrantResolution(
				feature: null, resourceType, AuthorizationTelemetry.GrantLevelDeniedEarly);
			return OperationGrant.Denied;
		}

		// Bypass is always live — never cached. Admin promotion is immediate.
		if (await grantResolver.ShouldBypassAsync(context, cancellationToken).ConfigureAwait(false)) {
			AuthorizationTelemetry.RecordGrantResolution(
				feature: null, resourceType, AuthorizationTelemetry.GrantLevelBypass);
			return OperationGrant.Unrestricted;
		}

		var feature = context.DomainFeature ?? "unknown";
		var grants = context.RequiredGrants;

		if (grants.Count == 0) {
			AuthorizationTelemetry.RecordGrantResolution(
				feature, resourceType, AuthorizationTelemetry.GrantLevelDeniedEarly);
			return OperationGrant.Denied;
		}

		var callerId = context.UserState.Id;
		var cacheKey = OperationGrantCacheKeys.BuildKey(
			cacheSettings.Version,
			callerId,
			feature,
			grants);

		// L1: scoped memoization
		if (this._grantCache.TryGetValue(cacheKey, out var cached)) {
			AuthorizationTelemetry.RecordGrantResolution(
				feature, resourceType, AuthorizationTelemetry.GrantLevelL1Hit);
			return cached;
		}

		// L2: cross-request cache. When provider is None, NoCacheService
		// executes the factory directly — no branching needed.
		var l2Start = Timing.Start();
		var grant = await this.CreateWithL2CacheAsync(context, cacheKey, callerId, feature, cancellationToken)
			.ConfigureAwait(false);
		AuthorizationTelemetry.RecordGrantResolution(
			feature, resourceType, AuthorizationTelemetry.GrantLevelL2,
			durationMs: Timing.GetElapsedMilliseconds(l2Start));

		this._grantCache[cacheKey] = grant;
		return grant;
	}

	// L2 cache integration ————————————————————————————————————

	private async ValueTask<OperationGrant> CreateWithL2CacheAsync<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> context,
		string cacheKey,
		string callerId,
		string domainFeature,
		CancellationToken cancellationToken)
		where TAuthorizableObject : IAuthorizableObject {

		var tags = OperationGrantCacheKeys.BuildTags(callerId, domainFeature);
		var settings = this.BuildEffectiveCacheSettings(domainFeature);

		return await cacheService.GetOrCreateAsync(
			cacheKey,
			async ct => await this.CreateFromGrantResolverAsync(context, ct).ConfigureAwait(false),
			settings,
			tags,
			cancellationToken).ConfigureAwait(false);
	}

	// Cold-path resolution ————————————————————————————————————

	private async ValueTask<OperationGrant> CreateFromGrantResolverAsync<TAuthorizableObject>(
		AuthorizationContext<TAuthorizableObject> context,
		CancellationToken cancellationToken)
		where TAuthorizableObject : IAuthorizableObject {

		var granted = await grantResolver
			.ResolveGrantsAsync(context, cancellationToken)
			.ConfigureAwait(false);

		var homeOwner = await grantResolver
			.ResolveHomeOwnerAsync(context, cancellationToken)
			.ConfigureAwait(false);

		var combined = Combine(granted.OwnerIds, homeOwner);
		return combined.Count == 0
			? OperationGrant.Denied
			: OperationGrant.ForOwners(combined, granted.Extensions);
	}

	// Cache configuration helpers —————————————————————————————

	private CacheExpirationSettings BuildEffectiveCacheSettings(string domainFeature) {
		// Cascade: domain override → grant-level default → root CacheSettings default
		var defaults = rootCacheSettings.DefaultExpiration;

		var expiration = cacheSettings.Expiration ?? defaults.Expiration;
		if (cacheSettings.FeatureOverrides.TryGetValue(domainFeature, out var ov) &&
			ov.Expiration.HasValue) {
			expiration = ov.Expiration.Value;
		}

		return new CacheExpirationSettings(Expiration: expiration);
	}

	// Owner merge ————————————————————————————————————————————

	private static IReadOnlyList<string> Combine(IReadOnlyList<string> grantedOwners, string? homeOwner) {
		ArgumentNullException.ThrowIfNull(grantedOwners);

		if (string.IsNullOrEmpty(homeOwner)) {
			return grantedOwners;
		}

		// Quick path: home owner already present.
		for (var i = 0; i < grantedOwners.Count; i++) {
			if (string.Equals(grantedOwners[i], homeOwner, StringComparison.Ordinal)) {
				return grantedOwners;
			}
		}

		var merged = new List<string>(grantedOwners.Count + 1);
		merged.AddRange(grantedOwners);
		merged.Add(homeOwner);
		return merged;
	}
}
