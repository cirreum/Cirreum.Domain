namespace Cirreum.State;
/// <summary>
/// Abstract base class for state that provides notification scoping and batching functionality.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class to get automatic notification batching capabilities.
/// Alternatively, implement <see cref="IScopedNotificationState"/> directly if you need
/// custom notification behavior.
/// </para>
/// <para>
/// The scoping mechanism supports nested operations, ensuring that notifications are only
/// triggered when the outermost scope completes, similar to transaction boundaries in databases.
/// </para>
/// </remarks>
public abstract class ScopedNotificationState : IScopedNotificationState {

	private int _scopeCount;

	// -------------------------------------------------------------------------
	// Scope Factory
	// -------------------------------------------------------------------------

	/// <inheritdoc/>
	public IDisposable CreateNotificationScope() {
		this.StartNewScope();
		return new NotificationScope(this.EndScopeAndAttemptNotify);
	}

	// -------------------------------------------------------------------------
	// Overridable Notification Hook
	// -------------------------------------------------------------------------

	/// <summary>
	/// Invoked after the state has changed and notifications should be dispatched.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Override this method to implement state-specific notification logic. Implementations
	/// typically call <c>stateManager.NotifySubscribers&lt;TStateInterface&gt;(this)</c>
	/// to notify subscribers such as UI components or other state observers.
	/// </para>
	/// <para>
	/// This method is invoked by <see cref="NotifyStateChanged"/> when no notification
	/// scopes are active. It is also invoked by <see cref="CreateNotificationScope"/>
	/// when the outermost notification scope completes.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// protected override void OnStateHasChanged() {
	///     stateManager.NotifySubscribers&lt;IMyState&gt;(this);
	/// }
	/// </code>
	/// </example>
	protected abstract void OnStateHasChanged();

	// -------------------------------------------------------------------------
	// Protected Notify Trigger
	// -------------------------------------------------------------------------

	/// <summary>
	/// Triggers a state change notification.
	/// </summary>
	/// <remarks>
	/// This method should be called from state mutation methods to signal that the
	/// state has changed.
	/// <para>
	/// If one or more notification scopes are active, the notification is suppressed.
	/// </para>
	/// </remarks>
	protected virtual void NotifyStateChanged() {
		if (this._scopeCount > 0) {
			return;
		}
		this.OnStateHasChanged();
	}

	// -------------------------------------------------------------------------
	// Internal Scope Tracking
	// -------------------------------------------------------------------------

	private void StartNewScope() => Interlocked.Increment(ref this._scopeCount);

	private void EndScopeAndAttemptNotify() {
		var count = Interlocked.Decrement(ref this._scopeCount);
		if (count == 0) {
			this.OnStateHasChanged();
		} else if (count < 0) {
			throw new InvalidOperationException("Notification scope ended without a matching start.");
		}
	}

	// -------------------------------------------------------------------------
	// Scope Token
	// -------------------------------------------------------------------------

	/// <summary>
	/// Notification scope token. Calls the end-scope action on dispose.
	/// </summary>
	private sealed class NotificationScope(Action endScope) : IDisposable {
		private bool _isDisposed;

		public void Dispose() {
			if (!this._isDisposed) {
				this._isDisposed = true;
				endScope();
			}
		}
	}

}
