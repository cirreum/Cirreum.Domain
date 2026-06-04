namespace Cirreum.Authorization.Resources;

using Cirreum.Authorization.Diagnostics;
using Cirreum.Exceptions;
using Cirreum.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

/// <summary>
/// Sealed implementation of <see cref="IResourceAccessEvaluator"/>. Resolves effective
/// access by walking the resource hierarchy via <see cref="IAccessEntryProvider{T}"/> and
/// caching results per-request in an L1 dictionary.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <b>Scoped</b> — the L1 cache lives for a single request. The caller's
/// identity and effective roles are read from <see cref="IAuthorizationContextAccessor"/>,
/// which the authorization pipeline populates before the handler runs.
/// </para>
/// </remarks>
internal sealed class ResourceAccessEvaluator(
	IAuthorizationContextAccessor authorizationContextAccessor,
	IServiceProvider services,
	ILogger<ResourceAccessEvaluator> logger
) : IResourceAccessEvaluator {

	// L1 per-request cache keyed by "{TypeName}:{ResourceId}"
	private readonly Dictionary<string, EffectiveAccess> _cache = [];

	/// <inheritdoc/>
	public async ValueTask<Result> CheckAsync<T>(
		T resource,
		Permission permission,
		CancellationToken cancellationToken = default)
		where T : IProtectedResource {

		var (userState, effectiveRoles) = this.ResolveCaller();

		if (effectiveRoles.Count == 0) {
			logger.LogResourceAccessDenied(
				userState.Name,
				typeof(T).Name,
				resource.ResourceId,
				permission.ToString(),
				DenyCodes.ResourceAccessDenied);

			EmitTelemetry(typeof(T).Name, AuthorizationTelemetry.DecisionDeny, DenyCodes.ResourceAccessDenied);
			return Result.Fail(new ForbiddenAccessException(
				$"User '{userState.Name}' has no roles — access denied to {typeof(T).Name}."));
		}

		var provider = services.GetRequiredService<IAccessEntryProvider<T>>();
		var effective = await this.ResolveEffectiveAccessAsync(resource, provider, cancellationToken).ConfigureAwait(false);

		if (effective.IsAuthorized(permission, effectiveRoles)) {
			logger.LogResourceAccessAllowed(
				userState.Name,
				typeof(T).Name,
				resource.ResourceId,
				permission);

			EmitTelemetry(typeof(T).Name, AuthorizationTelemetry.DecisionPass, AuthorizationTelemetry.ReasonPass);
			return Result.Success;
		}

		logger.LogResourceAccessDenied(
			userState.Name,
			typeof(T).Name,
			resource.ResourceId,
			permission.ToString(),
			DenyCodes.ResourceAccessDenied);

		EmitTelemetry(typeof(T).Name, AuthorizationTelemetry.DecisionDeny, DenyCodes.ResourceAccessDenied);
		return Result.Fail(new ForbiddenAccessException(
			$"User '{userState.Name}' does not have '{permission}' on {typeof(T).Name} '{resource.ResourceId}'."));
	}

	/// <inheritdoc/>
	public async ValueTask<Result> CheckAsync<T>(
		string? resourceId,
		Permission permission,
		CancellationToken cancellationToken = default)
		where T : IProtectedResource {

		var provider = services.GetRequiredService<IAccessEntryProvider<T>>();

		// null resourceId → root defaults
		if (resourceId is null) {
			var rootAccess = new EffectiveAccess(provider.RootDefaults);
			var (userState, effectiveRoles) = this.ResolveCaller();

			if (effectiveRoles.Count == 0) {
				EmitTelemetry(typeof(T).Name, AuthorizationTelemetry.DecisionDeny, DenyCodes.ResourceAccessDenied);
				return Result.Fail(new ForbiddenAccessException(
					$"User '{userState.Name}' has no roles — access denied to {typeof(T).Name}."));
			}

			if (rootAccess.IsAuthorized(permission, effectiveRoles)) {
				EmitTelemetry(typeof(T).Name, AuthorizationTelemetry.DecisionPass, AuthorizationTelemetry.ReasonPass);
				return Result.Success;
			}

			EmitTelemetry(typeof(T).Name, AuthorizationTelemetry.DecisionDeny, DenyCodes.ResourceAccessDenied);
			return Result.Fail(new ForbiddenAccessException(
				$"User '{userState.Name}' does not have '{permission}' at root of {typeof(T).Name}."));
		}

		var resource = await provider.GetByIdAsync(resourceId, cancellationToken).ConfigureAwait(false);

		if (resource is null) {
			var (userState, _) = this.ResolveCaller();
			EmitTelemetry(typeof(T).Name, AuthorizationTelemetry.DecisionDeny, DenyCodes.ResourceNotFound);
			return Result.Fail(new NotFoundException(resourceId));
		}

		return await this.CheckAsync(resource, permission, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async ValueTask<IReadOnlyList<T>> FilterAsync<T>(
		IEnumerable<T> resources,
		Permission permission,
		CancellationToken cancellationToken = default)
		where T : IProtectedResource {

		var (_, effectiveRoles) = this.ResolveCaller();

		if (effectiveRoles.Count == 0) {
			return [];
		}

		var provider = services.GetRequiredService<IAccessEntryProvider<T>>();
		var result = new List<T>();

		foreach (var resource in resources) {
			cancellationToken.ThrowIfCancellationRequested();

			var effective = await this.ResolveEffectiveAccessAsync(resource, provider, cancellationToken).ConfigureAwait(false);
			if (effective.IsAuthorized(permission, effectiveRoles)) {
				result.Add(resource);
			}
		}

		return result;
	}

	// ———————————————————————— Private helpers ————————————————————————

	/// <summary>
	/// Returns the caller's resolved identity from the authorization context.
	/// The pipeline always runs before the handler, so the context is guaranteed
	/// to be populated by the time any handler calls into this evaluator.
	/// </summary>
	private (IUserState UserState, IImmutableSet<Role> EffectiveRoles) ResolveCaller() {
		var authContext = authorizationContextAccessor.Current
			?? throw new InvalidOperationException(
				"IAuthorizationContextAccessor.Current is null. "
				+ "ResourceAccessEvaluator requires the authorization pipeline to have run.");

		return (authContext.UserState, authContext.EffectiveRoles);
	}

	/// <summary>
	/// Resolves the effective access for a resource by walking the hierarchy.
	/// Uses L1 cache to avoid redundant walks (sibling optimization).
	/// </summary>
	private async ValueTask<EffectiveAccess> ResolveEffectiveAccessAsync<T>(
		T resource,
		IAccessEntryProvider<T> provider,
		CancellationToken cancellationToken)
		where T : IProtectedResource {

		var cacheKey = BuildCacheKey<T>(resource.ResourceId);

		// L1 cache check
		if (cacheKey is not null && this._cache.TryGetValue(cacheKey, out var cached)) {
			return cached;
		}

		// Start with the resource's own entries
		var entries = new List<AccessEntry>(resource.AccessList);

		// Walk up the hierarchy if inheritance is enabled
		if (resource.InheritPermissions) {
			var ancestorIds = resource.AncestorResourceIds;

			if (ancestorIds is { Count: > 0 }) {
				// ——— Batch path: materialized ancestor chain available ———
				await this.ResolveBatchAncestorsAsync(resource, ancestorIds, entries, provider, cancellationToken)
					.ConfigureAwait(false);
			} else {
				// ——— Legacy path: sequential walk ———
				await this.ResolveWalkAncestorsAsync(resource, entries, provider, cancellationToken)
					.ConfigureAwait(false);
			}
		} else if (provider.GetParentId(resource) is null) {
			// Resource is at root and doesn't inherit — still apply root defaults
			entries.AddRange(provider.RootDefaults);
		}

		var effective = new EffectiveAccess(entries);

		// Cache the result
		if (cacheKey is not null) {
			this._cache[cacheKey] = effective;
		}

		return effective;
	}

	/// <summary>
	/// Batch path — loads all ancestors in a single call using the materialized
	/// <see cref="IProtectedResource.AncestorResourceIds"/> chain.
	/// </summary>
	private async ValueTask ResolveBatchAncestorsAsync<T>(
		T resource,
		IReadOnlyList<string> ancestorIds,
		List<AccessEntry> entries,
		IAccessEntryProvider<T> provider,
		CancellationToken cancellationToken)
		where T : IProtectedResource {

		var typeName = typeof(T).Name;

		// Pre-scan for L1 cache hit — find the nearest cached ancestor.
		// Only batch-load ancestors before that point.
		var batchUntil = ancestorIds.Count;
		EffectiveAccess? cachedTail = null;
		for (var i = 0; i < ancestorIds.Count; i++) {
			var key = BuildCacheKey<T>(ancestorIds[i]);
			if (key is not null && this._cache.TryGetValue(key, out var hit)) {
				batchUntil = i;
				cachedTail = hit;
				break;
			}
		}

		// Batch-load the ancestors we actually need
		Dictionary<string, T> ancestorMap;
		if (batchUntil > 0) {
			var idsToLoad = batchUntil < ancestorIds.Count
				? [.. ancestorIds.Take(batchUntil)]
				: ancestorIds;

			var loaded = await provider.GetManyByIdAsync(idsToLoad, cancellationToken)
				.ConfigureAwait(false);

			logger.LogResourceAccessBatchLoaded(loaded.Count, typeName, resource.ResourceId);

			ancestorMap = new(loaded.Count, StringComparer.Ordinal);
			foreach (var ancestor in loaded) {
				if (ancestor.ResourceId is not null) {
					ancestorMap[ancestor.ResourceId] = ancestor;
				}
			}
		} else {
			// Immediate parent was cached — skip batch load entirely
			ancestorMap = [];
		}

		// Walk the ancestor chain in order (nearest-first), collecting entries
		// and tracking each ancestor's start index for reverse-caching
		var visited = new HashSet<string>(StringComparer.Ordinal);
		if (resource.ResourceId is not null) {
			visited.Add(resource.ResourceId);
		}

		var ancestorStartIndices = new List<(string Id, int StartIndex)>();
		var reachedRoot = false;

		for (var i = 0; i < ancestorIds.Count; i++) {
			cancellationToken.ThrowIfCancellationRequested();
			var ancestorId = ancestorIds[i];

			// Cycle detection
			if (!visited.Add(ancestorId)) {
				logger.LogResourceAccessCycleDetected(typeName, ancestorId);
				break;
			}

			// Cache hit at this position — append cached entries and stop
			if (i == batchUntil && cachedTail is not null) {
				entries.AddRange(cachedTail.Entries);
				reachedRoot = true; // cached entries include root defaults
				break;
			}

			// Look up the ancestor from the batch-loaded map
			if (!ancestorMap.TryGetValue(ancestorId, out var ancestor)) {
				logger.LogResourceAccessOrphanDetected(typeName, ancestorId);
				break;
			}

			ancestorStartIndices.Add((ancestorId, entries.Count));
			entries.AddRange(ancestor.AccessList);

			if (!ancestor.InheritPermissions) {
				reachedRoot = true;
				break;
			}

			// Last ancestor in the chain — we've reached the root
			if (i == ancestorIds.Count - 1) {
				reachedRoot = true;
			}
		}

		// Merge root defaults if we reached the root
		if (reachedRoot && cachedTail is null) {
			entries.AddRange(provider.RootDefaults);
		}

		// Reverse-cache each ancestor's effective access for sibling optimization.
		// For ancestor at position i, its effective = entries from its start index onward.
		for (var i = 0; i < ancestorStartIndices.Count; i++) {
			var (id, startIndex) = ancestorStartIndices[i];
			var key = BuildCacheKey<T>(id);
			if (key is not null) {
				this._cache[key] = new EffectiveAccess(entries.GetRange(startIndex, entries.Count - startIndex));
			}
		}
	}

	/// <summary>
	/// Legacy path — sequential walk using <see cref="IAccessEntryProvider{T}.GetByIdAsync"/>.
	/// </summary>
	private async ValueTask ResolveWalkAncestorsAsync<T>(
		T resource,
		List<AccessEntry> entries,
		IAccessEntryProvider<T> provider,
		CancellationToken cancellationToken)
		where T : IProtectedResource {

		var typeName = typeof(T).Name;
		var visited = new HashSet<string>(StringComparer.Ordinal);
		if (resource.ResourceId is not null) {
			visited.Add(resource.ResourceId);
		}

		var parentId = provider.GetParentId(resource);
		var reachedRoot = parentId is null;

		while (parentId is not null) {
			cancellationToken.ThrowIfCancellationRequested();

			// Cycle detection
			if (!visited.Add(parentId)) {
				logger.LogResourceAccessCycleDetected(typeName, parentId);
				break;
			}

			// Sibling optimization: check if parent was already resolved
			var parentCacheKey = BuildCacheKey<T>(parentId);
			if (parentCacheKey is not null && this._cache.TryGetValue(parentCacheKey, out var parentCached)) {
				entries.AddRange(parentCached.Entries);
				// Parent's effective already includes its ancestors + root defaults
				reachedRoot = true;
				break;
			}

			var parent = await provider.GetByIdAsync(parentId, cancellationToken).ConfigureAwait(false);

			if (parent is null) {
				// Orphan — parent doesn't exist; stop walking
				logger.LogResourceAccessOrphanDetected(typeName, parentId);
				break;
			}

			entries.AddRange(parent.AccessList);

			if (!parent.InheritPermissions) {
				// Inheritance broken at parent
				reachedRoot = true;
				break;
			}

			parentId = provider.GetParentId(parent);
			if (parentId is null) {
				reachedRoot = true;
			}
		}

		// Merge root defaults if we reached the root
		if (reachedRoot) {
			entries.AddRange(provider.RootDefaults);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string? BuildCacheKey<T>(string? resourceId) =>
		resourceId is not null ? $"{typeof(T).Name}:{resourceId}" : null;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void EmitTelemetry(string resourceType, string decision, string reason) {
		AuthorizationTelemetry.RecordDecision(
			AuthorizationTelemetry.StageResourceAccess,
			AuthorizationTelemetry.StepResourceAccessCheck,
			decision,
			reason,
			resourceType: resourceType);
	}
}
