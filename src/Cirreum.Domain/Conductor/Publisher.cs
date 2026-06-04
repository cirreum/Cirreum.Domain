namespace Cirreum.Conductor;

using Cirreum.Conductor.Internal;
using Microsoft.Extensions.Logging;

/// <summary>
/// Default publisher that sends notifications to all registered handlers.
/// Supports parallel, sequential and fire-and-forget publishing.
/// </summary>
sealed class Publisher(
	IServiceProvider serviceProvider,
	PublisherStrategy defaultStrategy,
	ILogger<Publisher> logger
) : IPublisher {

	public Task<Result> PublishAsync<TNotification>(
		TNotification notification,
		PublisherStrategy? strategy = null,
		CancellationToken cancellationToken = default)
		where TNotification : INotification {

		ArgumentNullException.ThrowIfNull(notification);

		var wrapper = TypeCache.NotificationHandlers.GetOrAdd(notification.GetType(), static nt => {
			var wrapperType = typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(nt);
			return (NotificationHandlerWrapper)(Activator.CreateInstance(wrapperType)
				?? throw new InvalidOperationException($"Could not create wrapper for {nt.Name}"));
		});

		return wrapper.Handle(
			this,
			logger,
			notification,
			serviceProvider,
			strategy,
			defaultStrategy,
			cancellationToken);
	}

	internal async Task<Result> PublishSequentialAsync<TNotification>(
		TNotification notification,
		INotificationHandler<TNotification>[] handlers,
		bool stopOnFailure,
		CancellationToken cancellationToken)
		where TNotification : INotification {

		List<Exception>? failures = null;

		foreach (var handler in handlers) {
			cancellationToken.ThrowIfCancellationRequested();

			var handlerType = handler.GetType();
			try {
				await handler.HandleAsync(notification, cancellationToken);
			} catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
				// Cooperative cancellation - let it bubble
				throw;
			} catch (Exception ex) {
				PublisherLogger.HandlerThrewException(logger, handlerType, ex);

				// Wrap exception with handler context
				var wrappedException = new InvalidOperationException(
					$"Handler {handlerType.Name} failed", ex);

				failures ??= [];
				failures.Add(wrappedException);

				if (stopOnFailure) {
					break;
				}
			}
		}

		if (failures is null) {
			return Result.Success;
		}

		var message = $"{failures.Count} notification handler(s) failed";
		return Result.Fail(new AggregateException(message, failures));
	}

	internal async Task<Result> PublishParallelAsync<TNotification>(
		TNotification notification,
		INotificationHandler<TNotification>[] handlers,
		CancellationToken cancellationToken)
		where TNotification : INotification {

		cancellationToken.ThrowIfCancellationRequested();

		var tasks = handlers
			.Select(handler => InvokeHandlerAsync(handler, notification, logger, cancellationToken))
			.ToArray();

		var results = await Task.WhenAll(tasks);

		var failures = results
			.Where(r => r.IsFailure)
			.Select(r => r.Error!)
			.ToList();

		if (failures.Count == 0) {
			return Result.Success;
		}

		var message = $"{failures.Count} notification handler(s) failed";
		return Result.Fail(new AggregateException(message, failures));

		static async Task<Result> InvokeHandlerAsync(
			INotificationHandler<TNotification> handler,
			TNotification notification,
			ILogger handlerLogger,
			CancellationToken token) {

			// call in a loop, via Select project
			// so we throw here if canceled
			token.ThrowIfCancellationRequested();

			try {
				await handler.HandleAsync(notification, token);
				return Result.Success;
			} catch (OperationCanceledException) when (token.IsCancellationRequested) {
				// Cooperative cancellation - let it bubble
				throw;
			} catch (Exception ex) {
				var ht = handler.GetType();
				PublisherLogger.HandlerThrewException(handlerLogger, ht, ex);
				var wrappedException = new InvalidOperationException(
					$"Handler {handler.GetType().Name} failed", ex);
				return Result.Fail(wrappedException);
			}
		}
	}

	internal Task<Result> PublishFireAndForgetAsync<TNotification>(
		TNotification notification,
		INotificationHandler<TNotification>[] handlers)
		where TNotification : INotification {

		// Fire and forget with parallel execution
		_ = Task.Run(async () => {
			var tasks = handlers.Select(async handler => {
				try {
					await handler.HandleAsync(notification, CancellationToken.None);
				} catch (Exception ex) {
					var handlerType = handler.GetType();
					PublisherLogger.HandlerFailedFireAndForget(logger, handlerType, ex);
				}
			});

			// Wait for all but don't propagate exceptions (already logged)
			try {
				await Task.WhenAll(tasks);
			} catch {
				// Swallow - individual exceptions already logged
			}
		}, CancellationToken.None);

		return Result.SuccessTask;
	}
}