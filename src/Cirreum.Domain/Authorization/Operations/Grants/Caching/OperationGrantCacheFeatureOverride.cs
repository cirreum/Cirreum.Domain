namespace Cirreum.Authorization.Operations.Grants.Caching;

/// <summary>
/// Per-feature override for <see cref="OperationGrantCacheSettings"/>. Fields that are
/// <see langword="null"/> fall through to the top-level defaults.
/// </summary>
public sealed class OperationGrantCacheFeatureOverride {

	/// <summary>
	/// Override the <see cref="OperationGrantCacheSettings.Expiration"/> for this feature.
	/// <see langword="null"/> inherits the global setting.
	/// </summary>
	public TimeSpan? Expiration { get; set; }
}
