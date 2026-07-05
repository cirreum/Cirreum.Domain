namespace Cirreum.Authorization.Operations.Grants.Caching;

using Cirreum.Authentication.Events;

/// <summary>
/// Reacts to <see cref="GrantsInvalidated"/> by evicting the subject's entire cached grant-resolution entry
/// via <see cref="IOperationGrantCacheInvalidator.InvalidateCallerAsync"/>.
/// </summary>
/// <remarks>
/// Always calls <see cref="IOperationGrantCacheInvalidator.InvalidateCallerAsync"/> only, regardless of
/// <see cref="GrantsInvalidated.AffectedRoles"/>/<see cref="GrantsInvalidated.AffectedPermissions"/>.
/// <see cref="IOperationGrantCacheInvalidator.InvalidateFeatureAsync"/> is a different, broader operation — it
/// evicts across every caller for a feature, not just this subject — so using it here in response to a
/// per-subject event would risk over-evicting unrelated callers' cache entries. Apps with their own handlers
/// may still use <see cref="GrantsInvalidated.AffectedRoles"/>/<see cref="GrantsInvalidated.AffectedPermissions"/>
/// for their own targeted invalidation.
/// </remarks>
internal sealed class GrantsInvalidatedCacheHandler(
	IOperationGrantCacheInvalidator invalidator
) : IAuthenticationEventHandler<GrantsInvalidated> {

	/// <inheritdoc />
	public ValueTask HandleAsync(GrantsInvalidated evt, CancellationToken cancellationToken = default) {
		ArgumentNullException.ThrowIfNull(evt);
		return invalidator.InvalidateCallerAsync(evt.Subject, cancellationToken);
	}

}
