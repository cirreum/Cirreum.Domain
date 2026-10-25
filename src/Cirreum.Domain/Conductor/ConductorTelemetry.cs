namespace Cirreum.Conductor;

/// <summary>
/// Constants for OpenTelemetry instrumentation.
/// </summary>
public static class ConductorTelemetry {
	// Metrics

	/// <summary>
	/// Metric: Total number of operations.
	/// </summary>
	public const string OperationsTotalMetric = "cirreum.conductor.operations";

	/// <summary>
	/// Metric: Total number of failed operations.
	/// </summary>
	public const string OperationsFailedTotalMetric = "cirreum.conductor.operations.failed";

	/// <summary>
	/// Metric: Total number of canceled operations.
	/// </summary>
	public const string OperationsCanceledTotalMetric = "cirreum.conductor.operations.canceled";

	/// <summary>
	/// Metric: Histogram of operation duration.
	/// </summary>
	public const string OperationsDurationHistogram = "cirreum.conductor.operation.duration";

	/// <summary>
	/// Metric: Total number of domain events published.
	/// </summary>
	public const string DomainEventsTotalMetric = "cirreum.conductor.domain_events";

	/// <summary>
	/// Metric: Total number of domain events whose handlers failed.
	/// </summary>
	public const string DomainEventsFailedTotalMetric = "cirreum.conductor.domain_events.failed";

	/// <summary>
	/// Metric: Total number of domain events published with no registered handler.
	/// </summary>
	public const string DomainEventsNoHandlersTotalMetric = "cirreum.conductor.domain_events.no_handlers";

	/// <summary>
	/// Metric: Histogram of domain-event publishing duration.
	/// </summary>
	public const string DomainEventsDurationHistogram = "cirreum.conductor.domain_event.duration";

	// Tags/Attributes

	/// <summary>
	/// Tag: Error type.
	/// </summary>
	public const string ErrorTypeTag = "error.type";

	/// <summary>
	/// Tag: Operation type.
	/// </summary>
	public const string OperationTypeTag = "operation.type";

	/// <summary>
	/// Tag: Does this operation have a response.
	/// </summary>
	public const string OperationHasResponseTag = "operation.has_response";

	/// <summary>
	/// Tag: Response type.
	/// </summary>
	public const string ResponseTypeTag = "response.type";

	/// <summary>
	/// Tag: Operation status (success/failure/canceled).
	/// </summary>
	public const string OperationStatusTag = "operation.status";

	/// <summary>
	/// Tag: Operation succeeded (true/false). Not recorded.
	/// </summary>
	public const string OperationSucceededTag = "operation.succeeded";

	/// <summary>
	/// Tag: Operation failed (true/false). Recorded on the dispatch span only.
	/// </summary>
	public const string OperationFailedTag = "operation.failed";

	/// <summary>
	/// Tag: Operation canceled (true/false). Recorded on the dispatch span only.
	/// </summary>
	public const string OperationCanceledTag = "operation.canceled";

	/// <summary>
	/// Tag: Domain event type.
	/// </summary>
	public const string DomainEventTypeTag = "domain_event.type";

	/// <summary>
	/// Tag: Publisher strategy used for a domain event.
	/// </summary>
	public const string DomainEventStrategyTag = "domain_event.strategy";

	/// <summary>
	/// Tag: Number of handlers a domain event was published to.
	/// </summary>
	public const string DomainEventHandlerCountTag = "domain_event.handler_count";

	/// <summary>
	/// Tag: Domain-event publishing status (success/failure/canceled).
	/// </summary>
	public const string DomainEventStatusTag = "domain_event.status";

	/// <summary>
	/// Tag: Domain-event publishing canceled (true/false). Recorded on the publish span only.
	/// </summary>
	public const string DomainEventCanceledTag = "domain_event.canceled";
}
