namespace Cirreum.Authorization.Operations.Grants;

/// <summary>
/// Default scoped holder for <see cref="OperationGrant"/> and Stage 1 outcome signals.
/// Backed by per-instance fields; no synchronization is required because each request
/// scope receives its own instance.
/// </summary>
sealed class DefaultOperationGrantAccessor : IOperationGrantAccessor {

	private OperationGrant? _grant;
	private bool _ownerWasAutoStamped;
	private bool _wasRead;

	public OperationGrant Current {
		get {
			this._wasRead = true;
			return this._grant ?? OperationGrant.Denied;
		}
	}

	public bool OwnerWasAutoStamped => this._ownerWasAutoStamped;

	public bool WasRead => this._wasRead;

	public void Set(OperationGrant grant) {
		ArgumentNullException.ThrowIfNull(grant);
		this._grant = grant;
	}

	public void MarkOwnerAutoStamped() {
		this._ownerWasAutoStamped = true;
	}
}
