namespace Cirreum.Domain.Tests.RemoteServices;

using Cirreum.RemoteServices;
using Microsoft.Extensions.Logging.Abstractions;

public class RemoteConnectionBaseTests {

	/// <summary>Test double exposing the protected surface.</summary>
	private sealed class TestConnection() : RemoteConnectionBase(NullLogger.Instance) {

		public override string ConnectionId => "test";

		public override Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public override Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

		public override IDisposable On<T>(string method, Func<T, Task> handler) => new NoopDisposable();

		public override Task SendAsync<T>(string method, T payload, CancellationToken cancellationToken = default) =>
			Task.CompletedTask;

		public void Transition(RemoteConnectionState newState) => this.TransitionTo(newState);

		public Task InvokeConnected(CancellationToken ct = default) => this.OnConnectedAsync(ct);

		public Task InvokeReconnected(CancellationToken ct = default) => this.OnReconnectedAsync(ct);

		private sealed class NoopDisposable : IDisposable {
			public void Dispose() { }
		}

	}

	// State machine ————————————————————————————————————————————

	[Fact]
	public void InitialState_IsDisconnected() {
		new TestConnection().State.Should().Be(RemoteConnectionState.Disconnected);
	}

	[Fact]
	public void Transition_UpdatesState() {
		var connection = new TestConnection();

		connection.Transition(RemoteConnectionState.Connecting);

		connection.State.Should().Be(RemoteConnectionState.Connecting);
	}

	[Fact]
	public void Transition_RaisesEventWithBothStates() {
		var connection = new TestConnection();
		RemoteConnectionStateChangedEventArgs? observed = null;
		connection.StateChanged += (_, e) => observed = e;

		connection.Transition(RemoteConnectionState.Connecting);

		observed.Should().NotBeNull();
		observed!.PreviousState.Should().Be(RemoteConnectionState.Disconnected);
		observed.NewState.Should().Be(RemoteConnectionState.Connecting);
	}

	[Fact]
	public void Transition_ToSameState_RaisesNothing() {
		var connection = new TestConnection();
		connection.Transition(RemoteConnectionState.Connected);

		var raised = 0;
		connection.StateChanged += (_, _) => raised++;
		connection.Transition(RemoteConnectionState.Connected);

		raised.Should().Be(0);
	}

	[Fact]
	public void Transition_ListenerThrows_StateStillAdvances() {
		var connection = new TestConnection();
		connection.StateChanged += (_, _) => throw new InvalidOperationException("listener");

		var act = () => connection.Transition(RemoteConnectionState.Connected);

		act.Should().NotThrow();
		connection.State.Should().Be(RemoteConnectionState.Connected);
	}

	[Fact]
	public void Transition_SequenceReportsContiguousStates() {
		var connection = new TestConnection();
		List<RemoteConnectionStateChangedEventArgs> observed = [];
		connection.StateChanged += (_, e) => observed.Add(e);

		connection.Transition(RemoteConnectionState.Connecting);
		connection.Transition(RemoteConnectionState.Connected);
		connection.Transition(RemoteConnectionState.Reconnecting);
		connection.Transition(RemoteConnectionState.Connected);

		observed.Select(e => e.NewState).Should().Equal(
			RemoteConnectionState.Connecting,
			RemoteConnectionState.Connected,
			RemoteConnectionState.Reconnecting,
			RemoteConnectionState.Connected);

		// Each event's PreviousState is the prior event's NewState — no gaps.
		observed.Zip(observed.Skip(1), (a, b) => (a.NewState, b.PreviousState))
			.Should().OnlyContain(pair => pair.NewState == pair.PreviousState);
	}

	// Concurrency — transports raise transitions from background callbacks ————

	[Fact]
	public async Task ConcurrentTransitions_EveryEventReportsARealTransition() {
		var connection = new TestConnection();
		var observed = new System.Collections.Concurrent.ConcurrentBag<RemoteConnectionStateChangedEventArgs>();
		connection.StateChanged += (_, e) => observed.Add(e);

		// Two states only, so every legitimate event must be one of the two flips.
		await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => {
			for (var n = 0; n < 250; n++) {
				connection.Transition(i % 2 == 0
					? RemoteConnectionState.Connected
					: RemoteConnectionState.Reconnecting);
			}
		})));

		observed.Should().OnlyContain(e => e.PreviousState != e.NewState);
		observed.Should().OnlyContain(e =>
			(e.PreviousState == RemoteConnectionState.Connected && e.NewState == RemoteConnectionState.Reconnecting) ||
			(e.PreviousState == RemoteConnectionState.Reconnecting && e.NewState == RemoteConnectionState.Connected) ||
			e.PreviousState == RemoteConnectionState.Disconnected);
	}

	[Fact]
	public async Task ConcurrentTransitions_ToTheSameState_RaiseExactlyOneEvent() {
		// The transition is a read-modify-write: without atomicity, several threads can all
		// observe Disconnected and each raise the same Disconnected -> Connected change.
		for (var attempt = 0; attempt < 50; attempt++) {
			var connection = new TestConnection();
			var raised = 0;
			connection.StateChanged += (_, _) => Interlocked.Increment(ref raised);

			using var gate = new Barrier(8);
			await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => {
				gate.SignalAndWait();
				connection.Transition(RemoteConnectionState.Connected);
			})));

			raised.Should().Be(1, "one logical transition raises one event");
		}
	}

	[Fact]
	public async Task ConcurrentTransitions_FinalStateMatchesLastApplied() {
		var connection = new TestConnection();

		await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => {
			for (var n = 0; n < 250; n++) {
				connection.Transition(RemoteConnectionState.Reconnecting);
			}
		})));
		connection.Transition(RemoteConnectionState.Connected);

		connection.State.Should().Be(RemoteConnectionState.Connected);
	}

	// Lifecycle hooks ——————————————————————————————————————————

	[Fact]
	public async Task OnConnectedAsync_DefaultsToNoop() {
		await new TestConnection().InvokeConnected();
	}

	[Fact]
	public async Task OnReconnectedAsync_DefaultsToNoop() {
		await new TestConnection().InvokeReconnected();
	}

}
