namespace Cirreum.Domain.Tests.Conductor;

using Cirreum.Conductor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public class PublisherTests {

	public sealed record PlainEvent : IDomainEvent;

	[PublishingStrategy(PublisherStrategy.FailFast)]
	public sealed record FailFastEvent : IDomainEvent;

	/// <summary>Appends its name to a shared log, then throws when told to.</summary>
	private sealed class RecordingHandler<TEvent>(string name, List<string> log, bool fails = false)
		: IDomainEventHandler<TEvent>
		where TEvent : IDomainEvent {

		public Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default) {
			lock (log) {
				log.Add(name);
			}
			return fails
				? Task.FromException(new InvalidOperationException($"{name} failed"))
				: Task.CompletedTask;
		}

	}

	private static Publisher CreatePublisher<TEvent>(
		PublisherStrategy defaultStrategy,
		params IDomainEventHandler<TEvent>[] handlers)
		where TEvent : IDomainEvent {

		var services = new ServiceCollection();
		foreach (var handler in handlers) {
			services.AddSingleton(handler);
		}
		return new Publisher(services.BuildServiceProvider(), defaultStrategy, NullLogger<Publisher>.Instance);
	}

	[Fact]
	public async Task Sequential_RunsEveryHandler_AndAggregatesFailures() {
		var log = new List<string>();
		var publisher = CreatePublisher<PlainEvent>(
			PublisherStrategy.Sequential,
			new RecordingHandler<PlainEvent>("a", log, fails: true),
			new RecordingHandler<PlainEvent>("b", log),
			new RecordingHandler<PlainEvent>("c", log, fails: true));

		var result = await publisher.PublishAsync(new PlainEvent());

		log.Should().Equal("a", "b", "c");
		result.IsFailure.Should().BeTrue();
		result.Error.Should().BeOfType<AggregateException>()
			.Which.InnerExceptions.Should().HaveCount(2);
	}

	[Fact]
	public async Task FailFast_StopsAtTheFirstFailure() {
		var log = new List<string>();
		var publisher = CreatePublisher<PlainEvent>(
			PublisherStrategy.FailFast,
			new RecordingHandler<PlainEvent>("a", log),
			new RecordingHandler<PlainEvent>("b", log, fails: true),
			new RecordingHandler<PlainEvent>("c", log));

		var result = await publisher.PublishAsync(new PlainEvent());

		log.Should().Equal("a", "b");
		result.Error.Should().BeOfType<AggregateException>()
			.Which.InnerExceptions.Should().ContainSingle();
	}

	[Fact]
	public async Task Parallel_RunsEveryHandler_AndAggregatesFailures() {
		var log = new List<string>();
		var publisher = CreatePublisher<PlainEvent>(
			PublisherStrategy.Parallel,
			new RecordingHandler<PlainEvent>("a", log, fails: true),
			new RecordingHandler<PlainEvent>("b", log),
			new RecordingHandler<PlainEvent>("c", log, fails: true));

		var result = await publisher.PublishAsync(new PlainEvent());

		log.Should().BeEquivalentTo(["a", "b", "c"]);
		result.Error.Should().BeOfType<AggregateException>()
			.Which.InnerExceptions.Should().HaveCount(2);
	}

	[Theory]
	[InlineData(PublisherStrategy.Sequential)]
	[InlineData(PublisherStrategy.FailFast)]
	[InlineData(PublisherStrategy.Parallel)]
	public async Task Cancellation_Throws(PublisherStrategy strategy) {
		var log = new List<string>();
		var publisher = CreatePublisher<PlainEvent>(strategy, new RecordingHandler<PlainEvent>("a", log));
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		var act = () => publisher.PublishAsync(new PlainEvent(), cancellationToken: cts.Token);

		await act.Should().ThrowAsync<OperationCanceledException>();
		log.Should().BeEmpty();
	}

	[Fact]
	public async Task NoHandlers_Succeeds() {
		var publisher = CreatePublisher<PlainEvent>(PublisherStrategy.Sequential);

		var result = await publisher.PublishAsync(new PlainEvent());

		result.IsSuccess.Should().BeTrue();
	}

	[Fact]
	public async Task AttributeStrategy_OverridesTheDefault() {
		var log = new List<string>();
		var publisher = CreatePublisher<FailFastEvent>(
			PublisherStrategy.Sequential,
			new RecordingHandler<FailFastEvent>("a", log, fails: true),
			new RecordingHandler<FailFastEvent>("b", log));

		await publisher.PublishAsync(new FailFastEvent());

		log.Should().Equal("a");
	}

	[Fact]
	public async Task ExplicitStrategy_OverridesTheAttribute() {
		var log = new List<string>();
		var publisher = CreatePublisher<FailFastEvent>(
			PublisherStrategy.Sequential,
			new RecordingHandler<FailFastEvent>("a", log, fails: true),
			new RecordingHandler<FailFastEvent>("b", log));

		await publisher.PublishAsync(new FailFastEvent(), PublisherStrategy.Sequential);

		log.Should().Equal("a", "b");
	}

	[Fact]
	public async Task FireAndForget_ReturnsSuccess_AndStillRunsTheHandlers() {
		var log = new List<string>();
		var ran = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var publisher = CreatePublisher<PlainEvent>(
			PublisherStrategy.FireAndForget,
			new RecordingHandler<PlainEvent>("a", log, fails: true),
			new SignalingHandler(ran));

		var result = await publisher.PublishAsync(new PlainEvent());

		result.IsSuccess.Should().BeTrue();
		await ran.Task.WaitAsync(TimeSpan.FromSeconds(5));
	}

	private sealed class SignalingHandler(TaskCompletionSource ran) : IDomainEventHandler<PlainEvent> {
		public Task HandleAsync(PlainEvent domainEvent, CancellationToken cancellationToken = default) {
			ran.TrySetResult();
			return Task.CompletedTask;
		}
	}

}
