namespace Cirreum.Authorization.Resources;

using System.Collections.Immutable;

/// <summary>
/// Computed snapshot of the resolved access entries for a resource — its own ACL merged
/// with ancestor ACLs (when inheritance is enabled). Serves as the L1 cache value in
/// <see cref="ResourceAccessEvaluator"/>.
/// </summary>
internal sealed class EffectiveAccess(IReadOnlyList<AccessEntry> entries) {

	/// <summary>
	/// The merged set of access entries (own + inherited).
	/// </summary>
	public IReadOnlyList<AccessEntry> Entries => entries;

	/// <summary>
	/// Returns <see langword="true"/> when any entry grants <paramref name="permission"/>
	/// to a role contained in <paramref name="effectiveRoles"/>.
	/// </summary>
	/// <param name="permission">The permission to check.</param>
	/// <param name="effectiveRoles">The caller's resolved role set (includes inherited roles).</param>
	public bool IsAuthorized(Permission permission, IImmutableSet<Role> effectiveRoles) {
		for (var i = 0; i < entries.Count; i++) {
			var entry = entries[i];
			if (entry.HasPermission(permission) && effectiveRoles.Contains(entry.Role)) {
				return true;
			}
		}
		return false;
	}
}
