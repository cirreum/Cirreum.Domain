namespace Cirreum.Authorization.Operations.Grants.Caching;

/// <summary>
/// Configuration for the built-in grant cache. Bound from
/// <c>Cirreum:Authorization:Grants:Cache</c> in application settings.
/// </summary>
/// <remarks>
/// <para>
/// The grant cache stores computed <see cref="OperationGrant"/> results per caller and
/// permission set. It operates at two levels: L1 (scoped in-memory dictionary per DI
/// scope) and L2 (cross-request via <c>ICacheService</c>).
/// </para>
/// <para>
/// Settings can be overridden per feature via <see cref="FeatureOverrides"/>, keyed by
/// the namespace-derived feature name (e.g., <c>"issues"</c>).
/// </para>
/// </remarks>
public sealed class OperationGrantCacheSettings {

	/// <summary>
	/// The configuration section path for binding.
	/// </summary>
	public const string SectionPath = "Cirreum:Authorization:Grants:Cache";

	/// <summary>
	/// Cache key version. Changing this value effectively invalidates all existing cache
	/// entries without requiring an explicit purge — entries with the old version simply
	/// miss. Useful for schema evolution or emergency cache busting via environment
	/// variables in Azure Container Apps.
	/// </summary>
	public int Version { get; set; } = 1;

	/// <summary>
	/// Absolute expiration for L2 grant cache entries. If <see langword="null"/>,
	/// inherits from <see cref="Cirreum.Caching.CacheSettings.DefaultExpiration"/>.
	/// </summary>
	public TimeSpan? Expiration { get; set; }

	/// <summary>
	/// Per-feature overrides keyed by the namespace-derived feature name
	/// (e.g., <c>"issues"</c>, <c>"admin"</c>). Overrides are merged with the
	/// top-level settings; <see langword="null"/> fields fall through to the default.
	/// </summary>
	public Dictionary<string, OperationGrantCacheFeatureOverride> FeatureOverrides { get; set; } = [];
}
