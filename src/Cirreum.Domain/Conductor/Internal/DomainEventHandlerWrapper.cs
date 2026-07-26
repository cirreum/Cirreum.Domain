namespace Cirreum.Conductor.Internal;

using Microsoft.Extensions.Logging;

/// <summary>
/// Base wrapper for domainEvent handlers.
/// </summary>
internal abstract class DomainEventHandlerWrapper {
	public abstract Task<Result> Handle(
		Publisher publisher,
		ILogger logger,
		IDomainEvent domainEvent,
		IServiceProvider serviceProvider,
		PublisherStrategy? strategy,
		PublisherStrategy defaultStrategy,
		CancellationToken cancellationToken);
}