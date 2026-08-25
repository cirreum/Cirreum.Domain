namespace Cirreum.RemoteServices.Connections;

using Microsoft.Extensions.Logging;

/// <summary>
/// Abstract base class for <see cref="IRemoteConnection"/> implementations. Provides
/// state-management defaults, the <see cref="StateChanged"/> event plumbing, and a
/// transition helper that derived classes call when their underlying transport's state
/// changes.
/// </summary>
/// <remarks>
/// <para>
/// Transport-specific implementations derive from this and implement the abstract members
/// against a particular transport (a SignalR hub connection, a raw WebSocket, a gRPC stream).
/// The base does not own any transport state itself — it manages the public-surface state
/// machine, the lifecycle hooks, and event invocation so derived classes can focus on
/// transport-specific concerns.
/// </para>
/// <para>
/// Pairs with <see cref="RemoteClient"/> in the same family. Where <see cref="RemoteClient"/>
/// is the abstract base for request/response HTTP impls, this is the abstract base for
/// long-lived bidirectional connection impls.
/// </para>
/// </remarks>
public abstract class RemoteConnectionBase : IRemoteConnection {

	// Backed by an int so transitions can be applied atomically — transports raise state
	// changes from transport callbacks and background loops while callers may be inside
	// ConnectAsync or DisconnectAsync.
	private int _state = (int)RemoteConnectionState.Disconnected;

	/// <summary>Initializes a new instance with the supplied logger.</summary>
	protected RemoteConnectionBase(ILogger logger) {
		ArgumentNullException.ThrowIfNull(logger);
		this.Logger = logger;
	}

	/// <summary>Logger for derived-class diagnostics.</summary>
	protected ILogger Logger { get; }

	/// <inheritdoc/>
	public abstract string ConnectionId { get; }

	/// <inheritdoc/>
	public RemoteConnectionState State => (RemoteConnectionState)Volatile.Read(ref this._state);

	/// <inheritdoc/>
	public event EventHandler<RemoteConnectionStateChangedEventArgs>? StateChanged;

	/// <inheritdoc/>
	public abstract Task ConnectAsync(CancellationToken cancellationToken = default);

	/// <inheritdoc/>
	public abstract Task DisconnectAsync(CancellationToken cancellationToken = default);

	/// <inheritdoc/>
	public abstract IDisposable On<T>(string method, Func<T, Task> handler);

	/// <inheritdoc/>
	public abstract Task SendAsync<T>(string method, T payload, CancellationToken cancellationToken = default);

	/// <summary>
	/// Called after the transport has connected and before the connection reports
	/// <see cref="RemoteConnectionState.Connected"/>. The default implementation does nothing.
	/// </summary>
	/// <remarks>
	/// Runs on the initial connection only; see <see cref="OnReconnectedAsync"/> for the
	/// reconnect case. An exception thrown here fails the connect attempt.
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token for the connect operation.</param>
	protected virtual Task OnConnectedAsync(CancellationToken cancellationToken) =>
		Task.CompletedTask;

	/// <summary>
	/// Called after the transport has re-established a lost connection and before the
	/// connection reports <see cref="RemoteConnectionState.Connected"/> again. The default
	/// implementation does nothing.
	/// </summary>
	/// <remarks>
	/// The place to restore server-side session state that does not survive a transport
	/// reconnect, such as re-joining groups or re-announcing presence. An exception thrown
	/// here fails the reconnect attempt, leaving the transport's retry policy to continue.
	/// </remarks>
	/// <param name="cancellationToken">Cancellation token for the reconnect operation.</param>
	protected virtual Task OnReconnectedAsync(CancellationToken cancellationToken) =>
		Task.CompletedTask;

	/// <summary>
	/// Transition the public state and raise <see cref="StateChanged"/> if the transition
	/// is a real change. Derived classes call this when their transport's underlying state
	/// changes.
	/// </summary>
	/// <remarks>
	/// The transition is applied atomically, so every raised event reports the state the
	/// connection actually moved from. Concurrent transitions may raise their events in an
	/// order other than the one they were applied in; <see cref="State"/> is the authority
	/// on the current value.
	/// </remarks>
	protected void TransitionTo(RemoteConnectionState newState) {
		var previous = (RemoteConnectionState)Interlocked.Exchange(ref this._state, (int)newState);
		if (previous == newState) {
			return;
		}

		try {
			this.StateChanged?.Invoke(this, new RemoteConnectionStateChangedEventArgs(previous, newState));
		}
		catch (Exception ex) {
			// Listener exceptions must not corrupt the state machine.
			this.Logger.LogError(ex, "RemoteConnection state-change listener threw on transition {Previous} → {Next}.", previous, newState);
		}
	}

}
