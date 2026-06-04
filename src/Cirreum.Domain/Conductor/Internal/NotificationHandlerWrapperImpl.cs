namespace Cirreum.Conductor.Internal;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// Concrete wrapper implementation for typed notifications.
/// </summary>
internal sealed class NotificationHandlerWrapperImpl<TNotification>
	: NotificationHandlerWrapper
	where TNotification : INotification {

	private static readonly ConcurrentDictionary<Type, PublisherStrategy?> _strategyCache = new();
	private static readonly Type notificationType = typeof(TNotification);
	private static readonly string notificationTypeName = notificationType.Name;

	public override Task<Result> Handle(
		Publisher publisher,
		ILogger logger,
		INotification notification,
		IServiceProvider serviceProvider,
		PublisherStrategy? strategy,
		PublisherStrategy defaultStrategy,
		CancellationToken cancellationToken) {

		// Direct task return - no await, no extra state machine
		return HandleCoreAsync(
			publisher,
			logger,
			(TNotification)notification,
			serviceProvider,
			strategy,
			defaultStrategy,
			cancellationToken);

	}

	private static async Task<Result> HandleCoreAsync(
		Publisher publisher,
		ILogger logger,
		INotification notification,
		IServiceProvider serviceProvider,
		PublisherStrategy? strategy,
		PublisherStrategy defaultStrategy,
		CancellationToken cancellationToken) {

		// ----- 0. START TIMING & ACTIVITY -----
		using var activity = NotificationTelemetry.StartActivity(notificationTypeName);
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
				NotificationTelemetry.SetActivityCanceled(activity, (OperationCanceledException)error!);
				NotificationTelemetry.RecordCanceled(notificationTypeName, elapsed, (OperationCanceledException)error!);
			} else if (success) {
				NotificationTelemetry.SetActivitySuccess(activity);
				NotificationTelemetry.RecordSuccess(
					notificationTypeName,
					strategy,
					count,
					elapsed);
			} else {
				NotificationTelemetry.SetActivityError(activity, error!);
				NotificationTelemetry.RecordFailure(
					notificationTypeName,
					strategy,
					count,
					elapsed,
					error!);
			}
		}

		try {

			// ----- 1. RESOLVE HANDLERS -----
			var handlers = serviceProvider
				.GetServices<INotificationHandler<TNotification>>()
				.ToArray();

			handlerCount = handlers.Length;
			if (handlerCount == 0) {
				PublisherLogger.NoHandlersRegistered(logger, notificationTypeName);
				NotificationTelemetry.RecordNoHandlers(notificationTypeName);
				return Result.Success;
			}

			// ----- 2. DETERMINE STRATEGY -----
			if (strategy.HasValue) {
				effectiveStrategy = strategy.Value;
			} else {
				var attributeStrategy = _strategyCache.GetOrAdd(
					notificationType,
					static nt => nt.GetCustomAttribute<PublishingStrategyAttribute>()?.Strategy);
				effectiveStrategy = attributeStrategy ?? defaultStrategy;
			}

			// ----- 3. PUBLISH -----
			PublisherLogger.Publishing(logger, notificationTypeName, handlerCount, effectiveStrategy);
			var result = effectiveStrategy switch {
				PublisherStrategy.Sequential =>
					await publisher.PublishSequentialAsync((TNotification)notification, handlers, false, cancellationToken),
				PublisherStrategy.FailFast =>
					await publisher.PublishSequentialAsync((TNotification)notification, handlers, true, cancellationToken),
				PublisherStrategy.Parallel =>
					await publisher.PublishParallelAsync((TNotification)notification, handlers, cancellationToken),
				PublisherStrategy.FireAndForget =>
					await publisher.PublishFireAndForgetAsync((TNotification)notification, handlers),
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