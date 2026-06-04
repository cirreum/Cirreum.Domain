namespace Cirreum.Conductor;

using Microsoft.Extensions.Logging;
using System;

public static partial class PublisherLogger {

	[LoggerMessage(
		Level = LogLevel.Information,
		Message = "Publishing {NotificationType} to {HandlerCount} handlers using {Strategy} strategy")]
	public static partial void Publishing(
		ILogger logger,
		string notificationType,
		int handlerCount,
		PublisherStrategy strategy);

	[LoggerMessage(
		Level = LogLevel.Warning,
		Message = "No handlers registered for {NotificationType}")]
	public static partial void NoHandlersRegistered(
		ILogger logger,
		string notificationType);

	[LoggerMessage(
		Level = LogLevel.Error,
		Message = "Handler {HandlerType} threw an exception")]
	public static partial void HandlerThrewException(
		ILogger logger,
		Type handlerType,
		Exception ex);

	[LoggerMessage(
		Level = LogLevel.Warning,
		Message = "Handler {HandlerType} failed")]
	public static partial void HandlerFailed(
		ILogger logger,
		Type handlerType,
		string? errorMessage);

	[LoggerMessage(
		Level = LogLevel.Warning,
		Message = "Handler {HandlerType} failed in fire-and-forget mode")]
	public static partial void HandlerFailedFireAndForget(
		ILogger logger,
		Type handlerType,
		Exception ex);

}