namespace Cirreum.Authorization.Diagnostics;

using Microsoft.Extensions.Logging;

internal static partial class AuthorizationLogging {

	[LoggerMessage(
		EventId = AuthorizationLogEventId.AuthorizingDeniedId,
		Level = LogLevel.Warning,
		Message = "User '{UserName}' was DENIED access to '{ObjectName}'.\r\n{DeniedReason}")]
	public static partial void LogAuthorizingDenied(
		this ILogger logger,
		string userName,
		string objectName,
		string deniedReason);


	[LoggerMessage(
		EventId = AuthorizationLogEventId.AuthorizingUnknownErrorId,
		Level = LogLevel.Error,
		Message = "Exception encountered while authorizing User '{UserName}' for '{ObjectName}'.\r\n{FailureReasons}")]
	public static partial void LogAuthorizingUnknownError(
		this ILogger logger,
		Exception ex,
		string userName,
		string objectName,
		string failureReasons);


	[LoggerMessage(
		EventId = AuthorizationLogEventId.AuthorizingAllowedId,
		Level = LogLevel.Information,
		Message = "User '{UserName}' was ALLOWED access to '{ObjectName}'.")]
	public static partial void LogAuthorizingAllowed(
		this ILogger logger,
		string userName,
		string objectName);

}
