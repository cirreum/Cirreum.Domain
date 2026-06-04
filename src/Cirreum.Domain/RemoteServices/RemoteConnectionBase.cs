namespace Cirreum.RemoteServices;

using Microsoft.Extensions.Logging;

/// <summary>
/// Abstract base class for <see cref="IRemoteConnection"/> implementations. Provides
/// state-management defaults, the <see cref="StateChanged"/> event plumbing, and a
/// transition helper that derived classes call when their underlying transport's state
/// changes.
/// </summary>
/// <remarks>
/// <para>
/// Host-specific concrete impls in the runtime layer derive from
/// this and implement the abstract members against a specific transport (SignalR
/// <c>HubConnection</c>, raw <c>ClientWebSocket</c>, gRPC streaming, etc.). The base does
/// not own any transport state itself — it only manages the public-surface state machine
/// and event invocation so derived classes can focus on transport-specific concerns.
/// </para>
/// <para>
/// Pairs with <see cref="RemoteClient"/> in the same family. Where <see cref="RemoteClient"/>
/// is the abstract base for request/response HTTP impls, this is the abstract base for
/// long-lived bidirectional connection impls.
/// </para>
/// </remarks>
public abstract class RemoteConnectionBase : IRemoteConnection {

	private RemoteConnectionState _state = RemoteConnectionState.Disconnected;

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
	public RemoteConnectionState State => this._state;

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
	/// Transition the public state and raise <see cref="StateChanged"/> if the transition
	/// is a real change. Derived classes call this when their transport's underlying state
	/// changes.
	/// </summary>
	protected void TransitionTo(RemoteConnectionState newState) {
		var previous = this._state;
		if (previous == newState) {
			return;
		}

		this._state = newState;

		try {
			this.StateChanged?.Invoke(this, new RemoteConnectionStateChangedEventArgs(previous, newState));
		}
		catch (Exception ex) {
			// Listener exceptions must not corrupt the state machine.
			this.Logger.LogError(ex, "RemoteConnection state-change listener threw on transition {Previous} → {Next}.", previous, newState);
		}
	}

}
