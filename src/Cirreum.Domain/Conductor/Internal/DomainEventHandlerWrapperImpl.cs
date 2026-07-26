namespace Cirreum.Conductor.Internal;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// Concrete wrapper implementation for typed domain events.
/// </summary>
internal sealed class DomainEventHandlerWrapperImpl<TDomainEvent>
	: DomainEventHandlerWrapper
	where TDomainEvent : IDomainEvent {

	private static readonly ConcurrentDictionary<Type, PublisherStrategy?> _strategyCache = new();
	private static readonly Type DomainEventType = typeof(TDomainEvent);
	private static readonly string domainEventTypeName = DomainEventType.Name;

	public override Task<Result> Handle(
		Publisher publisher,
		ILogger logger,
		IDomainEvent domainEvent,
		IServiceProvider serviceProvider,
		PublisherStrategy? strategy,
		PublisherStrategy defaultStrategy,
		CancellationToken cancellationToken) {

		// Direct task return - no await, no extra state machine
		return HandleCoreAsync(
			publisher,
			logger,
			(TDomainEvent)domainEvent,
			serviceProvider,
			strategy,
			defaultStrategy,
			cancellationToken);

	}

	private static async Task<Result> HandleCoreAsync(
		Publisher publisher,
		ILogger logger,
		IDomainEvent domainEvent,
		IServiceProvider serviceProvider,
		PublisherStrategy? strategy,
		PublisherStrategy defaultStrategy,
		CancellationToken cancellationToken) {

		// ----- 0. START TIMING & ACTIVITY -----
		using var activity = DomainEventTelemetry.StartActivity(domainEventTypeName);
		var startTimestamp = activity is not null ? Timing.Start() : 0L;
		var effectiveStrategy = PublisherStrategy.Sequential;
		var handlerCount = 0;

		// Local function for recording telemetry
		void RecordTelemetry(bool success, int count = 0, PublisherStrategy strategy = PublisherStrategy.Sequential, Exception? error = null, bool canceled = false) {

			if (activity is null) {
				return;
			}

			var elapsed = Timing.GetElapsedMilliseconds(startTimestamp);

			if (canceled) {
				DomainEventTelemetry.SetActivityCanceled(activity, (OperationCanceledException)error!);
				DomainEventTelemetry.RecordCanceled(domainEventTypeName, elapsed, (OperationCanceledException)error!);
			} else if (success) {
				DomainEventTelemetry.SetActivitySuccess(activity);
				DomainEventTelemetry.RecordSuccess(
					domainEventTypeName,
					strategy,
					count,
					elapsed);
			} else {
				DomainEventTelemetry.SetActivityError(activity, error!);
				DomainEventTelemetry.RecordFailure(
					domainEventTypeName,
					strategy,
					count,
					elapsed,
					error!);
			}
		}

		try {

			// ----- 1. RESOLVE HANDLERS -----
			var handlers = serviceProvider
				.GetServices<IDomainEventHandler<TDomainEvent>>()
				.ToArray();

			handlerCount = handlers.Length;
			if (handlerCount == 0) {
				PublisherLogger.NoHandlersRegistered(logger, domainEventTypeName);
				DomainEventTelemetry.RecordNoHandlers(domainEventTypeName);
				return Result.Success;
			}

			// ----- 2. DETERMINE STRATEGY -----
			if (strategy.HasValue) {
				effectiveStrategy = strategy.Value;
			} else {
				var attributeStrategy = _strategyCache.GetOrAdd(
					DomainEventType,
					static nt => nt.GetCustomAttribute<PublishingStrategyAttribute>()?.Strategy);
				effectiveStrategy = attributeStrategy ?? defaultStrategy;
			}

			// ----- 3. PUBLISH -----
			PublisherLogger.Publishing(logger, domainEventTypeName, handlerCount, effectiveStrategy);
			var result = effectiveStrategy switch {
				PublisherStrategy.Sequential =>
					await publisher.PublishSequentialAsync((TDomainEvent)domainEvent, handlers, false, cancellationToken),
				PublisherStrategy.FailFast =>
					await publisher.PublishSequentialAsync((TDomainEvent)domainEvent, handlers, true, cancellationToken),
				PublisherStrategy.Parallel =>
					await publisher.PublishParallelAsync((TDomainEvent)domainEvent, handlers, cancellationToken),
				PublisherStrategy.FireAndForget =>
					await publisher.PublishFireAndForgetAsync((TDomainEvent)domainEvent, handlers),
				_ => Result.Fail(
					new InvalidOperationException($"Unknown publisher strategy: {effectiveStrategy}"))
			};

			// ----- 4. RECORD TELEMETRY -----
			RecordTelemetry(result.IsSuccess, handlerCount, effectiveStrategy, result.Error);

			return result;

		} catch (OperationCanceledException oce) {
			RecordTelemetry(false, handlerCount, effectiveStrategy, oce, true);
			throw;
		} catch (Exception fex) when (fex.IsFatal()) {
			throw;
		} catch (Exception ex) {
			RecordTelemetry(false, handlerCount, effectiveStrategy, ex);
			return Result.Fail(ex);
		}

	}

}