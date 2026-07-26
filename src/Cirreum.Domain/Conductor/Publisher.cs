namespace Cirreum.Conductor;

using Cirreum.Conductor.Internal;
using Microsoft.Extensions.Logging;

/// <summary>
/// Default publisher that sends domain events to all registered handlers.
/// Supports parallel, sequential and fire-and-forget publishing.
/// </summary>
sealed class Publisher(
	IServiceProvider serviceProvider,
	PublisherStrategy defaultStrategy,
	ILogger<Publisher> logger
) : IPublisher {

	public Task<Result> PublishAsync<TDomainEvent>(
		TDomainEvent domainEvent,
		PublisherStrategy? strategy = null,
		CancellationToken cancellationToken = default)
		where TDomainEvent : IDomainEvent {

		ArgumentNullException.ThrowIfNull(domainEvent);

		var wrapper = TypeCache.DomainEventHandlers.GetOrAdd(domainEvent.GetType(), static nt => {
			var wrapperType = typeof(DomainEventHandlerWrapperImpl<>).MakeGenericType(nt);
			return (DomainEventHandlerWrapper)(Activator.CreateInstance(wrapperType)
				?? throw new InvalidOperationException($"Could not create wrapper for {nt.Name}"));
		});

		return wrapper.Handle(
			this,
			logger,
			domainEvent,
			serviceProvider,
			strategy,
			defaultStrategy,
			cancellationToken);
	}

	internal async Task<Result> PublishSequentialAsync<TDomainEvent>(
		TDomainEvent domainEvent,
		IDomainEventHandler<TDomainEvent>[] handlers,
		bool stopOnFailure,
		CancellationToken cancellationToken)
		where TDomainEvent : IDomainEvent {

		List<Exception>? failures = null;

		foreach (var handler in handlers) {
			cancellationToken.ThrowIfCancellationRequested();

			var handlerType = handler.GetType();
			try {
				await handler.HandleAsync(domainEvent, cancellationToken);
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

		var message = $"{failures.Count} domainEvent handler(s) failed";
		return Result.Fail(new AggregateException(message, failures));
	}

	internal async Task<Result> PublishParallelAsync<TDomainEvent>(
		TDomainEvent domainEvent,
		IDomainEventHandler<TDomainEvent>[] handlers,
		CancellationToken cancellationToken)
		where TDomainEvent : IDomainEvent {

		cancellationToken.ThrowIfCancellationRequested();

		var tasks = handlers
			.Select(handler => InvokeHandlerAsync(handler, domainEvent, logger, cancellationToken))
			.ToArray();

		var results = await Task.WhenAll(tasks);

		var failures = results
			.Where(r => r.IsFailure)
			.Select(r => r.Error!)
			.ToList();

		if (failures.Count == 0) {
			return Result.Success;
		}

		var message = $"{failures.Count} domainEvent handler(s) failed";
		return Result.Fail(new AggregateException(message, failures));

		static async Task<Result> InvokeHandlerAsync(
			IDomainEventHandler<TDomainEvent> handler,
			TDomainEvent domainEvent,
			ILogger handlerLogger,
			CancellationToken token) {

			// call in a loop, via Select project
			// so we throw here if canceled
			token.ThrowIfCancellationRequested();

			try {
				await handler.HandleAsync(domainEvent, token);
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

	internal Task<Result> PublishFireAndForgetAsync<TDomainEvent>(
		TDomainEvent domainEvent,
		IDomainEventHandler<TDomainEvent>[] handlers)
		where TDomainEvent : IDomainEvent {

		// Fire and forget with parallel execution
		_ = Task.Run(async () => {
			var tasks = handlers.Select(async handler => {
				try {
					await handler.HandleAsync(domainEvent, CancellationToken.None);
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