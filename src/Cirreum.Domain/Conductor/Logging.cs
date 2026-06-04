namespace Cirreum.Conductor;

using Microsoft.Extensions.Logging;

internal static partial class Logging {

	/// <summary>
	/// Logs a failed Result with correlation tracking.
	/// </summary>
	/// <remarks>
	/// The error reference ID (operationId) can be provided to support teams for troubleshooting.
	/// </remarks>
	[LoggerMessage(
		EventId = LoggingEventId.ResultFailureId,
		Level = LogLevel.Warning,
		Message = "Operation '{OperationType}' failed with OperationId '{OperationId}' and CorrelationId: {CorrelationId}]. Exception: {ExceptionType} - {ExceptionMessage}")]
	public static partial void LogResultFailure(
		this ILogger logger,
		string operationType,
		string operationId,
		string correlationId,
		string exceptionType,
		string exceptionMessage);

	[LoggerMessage(
		EventId = LoggingEventId.SkippingAuthorizingId,
		Level = LogLevel.Debug,
		Message = "Skipped Authorizing Operation '{OperationName}' as it does not require authorization.")]
	public static partial void LogSkippedAuthorizingOperation(
		this ILogger logger,
		string operationName);

	[LoggerMessage(
		SkipEnabledCheck = true,
		EventId = LoggingEventId.LongRunningId,
		Level = LogLevel.Warning,
		Message = "Long running operation {OperationName} took {ElapsedMilliseconds}ms")]
	public static partial void LogLongRunningOperation(
		this ILogger logger,
		string operationName,
		long elapsedMilliseconds);

	[LoggerMessage(
		EventId = LoggingEventId.RecordTelemetryFailedId,
		Level = LogLevel.Error,
		Message = "Exception encountered recording telemetry.")]
	public static partial void LogRecordTelemetryFailed(
		this ILogger logger,
		Exception ex);

	[LoggerMessage(
		EventId = LoggingEventId.AuditLoggingFailedId,
		Level = LogLevel.Error,
		Message = "Exception enountered logging audit entry")]
	public static partial void LogAuditLoggingFailed(
		this ILogger logger,
		Exception ex);

}