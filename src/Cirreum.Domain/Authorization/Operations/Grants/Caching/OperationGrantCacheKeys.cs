namespace Cirreum.Authorization.Operations.Grants.Caching;

/// <summary>
/// Deterministic key and tag composition for the grant cache. Cache keys encode the
/// version, caller, feature, and sorted permission signature so that two
/// actions with the same permission set share a cache entry.
/// </summary>
/// <remarks>
/// Key format: <c>grant:v{version}:{callerId}:{feature}:{permissionSignature}</c>
/// <para>
/// Examples:
/// <c>grant:v1:user-123:issues:delete</c> -OR-
/// <c>grant:v1:user-123:issues:delete+write</c>
/// </para>
/// </remarks>
internal static class OperationGrantCacheKeys {

	/// <summary>
	/// Builds the full L2 cache key from the caller, feature, and resolved permissions.
	/// </summary>
	/// <remarks>
	/// Key format: <c>grant:v{version}:{callerId}:{feature}:{permissionSignature}</c>
	/// <para>
	/// Examples:
	/// <c>grant:v1:user-123:issues:delete</c> -OR-
	/// <c>grant:v1:user-123:issues:delete+write</c>
	/// </para>
	/// </remarks>
	internal static string BuildKey(
		int version,
		string callerId,
		string feature,
		PermissionSet permissions) =>
		$"grant:v{version}:{callerId}:{feature}:{SignatureOf(permissions)}";

	/// <summary>
	/// Order-independent signature of the operation verbs in <paramref name="permissions"/> —
	/// safe to embed in a key only because <see cref="RequiredGrantCache"/> guarantees every
	/// permission in the set shares the surrounding <c>feature</c> segment, so the feature is
	/// already scoped at the key level. Not a general-purpose signature.
	/// </summary>
	/// <returns>
	/// A <c>"+"</c>-joined string of ordinal-sorted operation names (e.g., <c>"archive+delete"</c>),
	/// the lone operation when the set has one element, or <see cref="string.Empty"/> when empty.
	/// </returns>
	internal static string SignatureOf(PermissionSet permissions) {
		if (permissions.Count == 0) {
			return string.Empty;
		}
		if (permissions.Count == 1) {
			return permissions[0].Operation;
		}

		var names = new string[permissions.Count];
		for (var i = 0; i < permissions.Count; i++) {
			names[i] = permissions[i].Operation;
		}

		// Sort operation names to normalize declaration order. The signature
		// encodes operations only — feature is already a separate segment of the
		// cache key, and a granted resource's permissions all share the same
		// feature by cross-feature validation. AND-semantics permissions are
		// commutative, so two operations declaring the same set of permissions
		// in different orders represent the same precondition and must produce
		// the same signature. Without sorting, identical-meaning operations
		// would miss each other's cache entries.
		Array.Sort(names, StringComparer.Ordinal);

		return string.Join('+', names);
	}

	/// <summary>
	/// Builds the tag set for a cache entry. Used for invalidation.
	/// </summary>
	internal static string[] BuildTags(string callerId, string feature) =>
		[CallerTag(callerId), FeatureTag(feature)];

	/// <summary>
	/// Tag for invalidating all entries for a specific caller.
	/// </summary>
	internal static string CallerTag(string callerId) =>
		$"grant:caller:{callerId}";

	/// <summary>
	/// Tag for invalidating all entries for a specific feature.
	/// </summary>
	internal static string FeatureTag(string feature) =>
		$"grant:feature:{feature}";
}
