namespace Cirreum.Authorization.Resources;

using Microsoft.Extensions.Logging;

/// <summary>
/// Source-generated <see cref="LoggerMessage"/> extensions for the resource access evaluator.
/// </summary>
internal static partial class ResourceAccessLogging {

	private const int EventBase = 10_100;

	[LoggerMessage(
		EventId = EventBase + 1,
		Level = LogLevel.Information,
		Message = "User '{UserName}' was ALLOWED '{Permission}' on {ResourceType} '{ResourceId}'.")]
	public static partial void LogResourceAccessAllowed(
		this ILogger logger,
		string userName,
		string resourceType,
		string? resourceId,
		string permission);

	[LoggerMessage(
		EventId = EventBase + 2,
		Level = LogLevel.Warning,
		Message = "User '{UserName}' was DENIED '{Permission}' on {ResourceType} '{ResourceId}' — {DenyCode}")]
	public static partial void LogResourceAccessDenied(
		this ILogger logger,
		string userName,
		string resourceType,
		string? resourceId,
		string permission,
		string denyCode);

	[LoggerMessage(
		EventId = EventBase + 3,
		Level = LogLevel.Warning,
		Message = "Cycle detected in {ResourceType} hierarchy at ResourceId '{ResourceId}' — stopping walk")]
	public static partial void LogResourceAccessCycleDetected(
		this ILogger logger,
		string resourceType,
		string resourceId);

	[LoggerMessage(
		EventId = EventBase + 4,
		Level = LogLevel.Warning,
		Message = "Orphan detected in {ResourceType} hierarchy — parent '{ParentId}' does not exist")]
	public static partial void LogResourceAccessOrphanDetected(
		this ILogger logger,
		string resourceType,
		string parentId);

	[LoggerMessage(
		EventId = EventBase + 5,
		Level = LogLevel.Debug,
		Message = "Batch-loaded {Count} ancestors for {ResourceType} '{ResourceId}'")]
	public static partial void LogResourceAccessBatchLoaded(
		this ILogger logger,
		int count,
		string resourceType,
		string? resourceId);
}
