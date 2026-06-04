namespace Cirreum.Conductor;

/// <summary>
/// Constants for OpenTelemetry instrumentation.
/// </summary>
public static class ConductorTelemetry {
	// Metrics

	/// <summary>
	/// Metric: Total number of operations.
	/// </summary>
	public const string OperationsTotalMetric = "conductor.operations.total";

	/// <summary>
	/// Metric: Total number of failed operations.
	/// </summary>
	public const string OperationsFailedTotalMetric = "conductor.operations.failed";

	/// <summary>
	/// Metric: Total number of canceled operations.
	/// </summary>
	public const string OperationsCanceledTotalMetric = "conductor.operations.canceled";

	/// <summary>
	/// Metric: Histogram of operation duration.
	/// </summary>
	public const string OperationsDurationHistogram = "conductor.operations.duration";

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
	/// Tag: Operation succeeded (true/false).
	/// </summary>
	public const string OperationSucceededTag = "operation.succeeded";

	/// <summary>
	/// Tag: Operation failed (true/false).
	/// </summary>
	public const string OperationFailedTag = "operation.failed";

	/// <summary>
	/// Tag: Operation canceled (true/false).
	/// </summary>
	public const string OperationCanceledTag = "operation.canceled";
}