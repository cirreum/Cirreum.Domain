namespace Cirreum.Conductor;

using Cirreum.Conductor.Internal;
using System;

/// <summary>
/// Default implementation of <see cref="IDispatcher"/> and <see cref="IConductor"/>
/// that routes operations to their handlers through a pipeline of intercepts,
/// and publishes notifications to all registered handlers.
/// </summary>
/// <remarks>
/// This dispatcher uses a wrapper-based caching strategy to avoid reflection overhead in the hot path.
/// Operation type wrappers are created once and cached for the lifetime of the application.
/// </remarks>
sealed class Dispatcher(
	IServiceProvider serviceProvider,
	IPublisher publisher
) : IConductor {

	#region IDispatcher Implementation

	/// <inheritdoc />
	public Task<Result> DispatchAsync<TOperation>(
		TOperation operation,
		CancellationToken cancellationToken = default)
		where TOperation : IOperation {

		ArgumentNullException.ThrowIfNull(operation);

		var wrapper = TypeCache.VoidHandlers.GetOrAdd(operation.GetType(), static operationType => {
			var wrapperType = typeof(OperationHandlerWrapperImpl<>).MakeGenericType(operationType);
			return (OperationHandlerWrapper)(Activator.CreateInstance(wrapperType)
				?? throw new InvalidOperationException($"Could not create wrapper for {operationType.Name}"));
		});

		return wrapper.HandleAsync(
			operation,
			serviceProvider,
			cancellationToken);
	}

	/// <inheritdoc />
	public Task<Result<TResultValue>> DispatchAsync<TResultValue>(
		IOperation<TResultValue> operation,
		CancellationToken cancellationToken = default) {

		ArgumentNullException.ThrowIfNull(operation);

		var wrapper = (OperationHandlerWrapper<TResultValue>)TypeCache.ResponseHandlers.GetOrAdd(
			operation.GetType(),
			static operationType => {
				var wrapperType = typeof(OperationHandlerWrapperImpl<,>)
					.MakeGenericType(operationType, typeof(TResultValue));
				return Activator.CreateInstance(wrapperType)
					?? throw new InvalidOperationException($"Could not create wrapper for {operationType.Name}");
			});

		return wrapper.HandleAsync(
			operation,
			serviceProvider,
			cancellationToken);
	}

	#endregion

	#region IPublisher Implementation

	/// <inheritdoc />
	public Task<Result> PublishAsync<TNotification>(
		TNotification notification,
		PublisherStrategy? strategy = null,
		CancellationToken cancellationToken = default)
		where TNotification : INotification {

		return publisher.PublishAsync(notification, strategy, cancellationToken);
	}

	#endregion
}