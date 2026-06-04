namespace Cirreum.RemoteServices;

using Microsoft.Extensions.Logging;

/// <summary>
/// Source-generated logging methods for RemoteClientTelemetry.
/// </summary>
internal static partial class RemoteClientLogging {

	[LoggerMessage(
		EventId = 1001,
		Level = LogLevel.Information,
		Message = "Completed {HttpMethod} {Endpoint} in {DurationMs}ms with status {StatusCode}")]
	internal static partial void LogRequestCompleted(
		this ILogger logger,
		string httpMethod,
		string endpoint,
		double durationMs,
		int statusCode);

	[LoggerMessage(
		EventId = 1002,
		Level = LogLevel.Error,
		Message = "Failed {HttpMethod} {Endpoint} after {DurationMs}ms with status {StatusCode}")]
	internal static partial void LogRequestFailed(
		this ILogger logger,
		Exception exception,
		string httpMethod,
		string endpoint,
		double durationMs,
		int? statusCode);

	[LoggerMessage(
		EventId = 1003,
		Level = LogLevel.Warning,
		Message = "Canceled {HttpMethod} {Endpoint} after {DurationMs}ms")]
	internal static partial void LogRequestCanceled(
		this ILogger logger,
		string httpMethod,
		string endpoint,
		double durationMs);

	[LoggerMessage(
		EventId = 1004,
		Level = LogLevel.Debug,
		Message = "Starting {HttpMethod} {Endpoint}")]
	internal static partial void LogRequestStarting(
		this ILogger logger,
		string httpMethod,
		string endpoint);

}