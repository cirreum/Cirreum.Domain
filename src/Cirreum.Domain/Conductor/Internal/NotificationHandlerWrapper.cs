namespace Cirreum.Conductor.Internal;

using Microsoft.Extensions.Logging;

/// <summary>
/// Base wrapper for notification handlers.
/// </summary>
internal abstract class NotificationHandlerWrapper {
	public abstract Task<Result> Handle(
		Publisher publisher,
		ILogger logger,
		INotification notification,
		IServiceProvider serviceProvider,
		PublisherStrategy? strategy,
		PublisherStrategy defaultStrategy,
		CancellationToken cancellationToken);
}