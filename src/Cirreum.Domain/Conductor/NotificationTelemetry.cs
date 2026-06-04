namespace Cirreum.Conductor;

using Cirreum.Diagnostics;
using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Provides telemetry capabilities for notification publishing.
/// </summary>
internal static class NotificationTelemetry {

	private static readonly ActivitySource _activitySource =
		new(CirreumTelemetry.ActivitySources.ConductorPublisher, CirreumTelemetry.Version);

	private static readonly Meter _meter =
		new(CirreumTelemetry.Meters.ConductorPublisher, CirreumTelemetry.Version);

	private static readonly Counter<long> _notificationCounter = _meter.CreateCounter<long>(
		"conductor.notifications.total",
		description: "Total number of notifications published");

	private static readonly Counter<long> _notificationFailedCounter = _meter.CreateCounter<long>(
		"conductor.notifications.failed.total",
		description: "Total number of failed notifications");

	private static readonly Counter<long> _notificationNoHandlersCounter = _meter.CreateCounter<long>(
		"conductor.notifications.no_handlers.total",
		description: "Total number of notifications with no handlers");

	private static readonly Histogram<double> _notificationDuration = _meter.CreateHistogram<double>(
		"conductor.notifications.duration",
		unit: "ms",
		description: "Notification publishing duration in milliseconds");

	#region Activity Management

	internal static Activity? StartActivity(string notificationName) {
		var activity = _activitySource.StartActivity(
			"Publish Notification",
			DomainContext.CurrentActivityKind);

		activity?.SetTag("notification.type", notificationName);

		return activity;
	}

	internal static void StopActivity(Activity? activity) {
		if (activity is not null) {
			activity.Stop();
			activity.Dispose();
		}
	}

	internal static void SetActivitySuccess(Activity? activity) {
		activity?.SetStatus(ActivityStatusCode.Ok);
	}

	internal static void SetActivityError(Activity? activity, Exception ex) {
		if (activity is not null) {
			activity.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity.SetTag("error.type", ex.GetType().Name);
			activity.AddException(ex);
		}
	}

	internal static void SetActivityCanceled(Activity? activity, OperationCanceledException oce) {
		if (activity is not null) {
			activity.SetStatus(ActivityStatusCode.Error, "Canceled");
			activity.SetTag("notification.canceled", true);
			activity.AddException(oce);
		}
	}

	#endregion

	#region Metrics Recording

	internal static void RecordSuccess(
		string notificationName,
		PublisherStrategy strategy,
		int handlerCount,
		double durationMs) {

		var tags = new TagList {
			{ "notification.type", notificationName },
			{ "notification.strategy", strategy.ToString() },
			{ "notification.handler_count", handlerCount },
			{ "notification.status", "success" }
		};

		_notificationCounter.Add(1, tags);
		_notificationDuration.Record(durationMs, tags);
	}

	internal static void RecordFailure(
		string notificationName,
		PublisherStrategy strategy,
		int handlerCount,
		double durationMs,
		Exception error) {

		var tags = new TagList {
			{ "notification.type", notificationName },
			{ "notification.strategy", strategy.ToString() },
			{ "notification.handler_count", handlerCount },
			{ "notification.status", "failure" },
			{ "error.type", error.GetType().Name }
		};

		_notificationCounter.Add(1, tags);
		_notificationFailedCounter.Add(1, tags);
		_notificationDuration.Record(durationMs, tags);
	}

	internal static void RecordCanceled(
		string notificationName,
		double durationMs,
		OperationCanceledException oce) {

		var tags = new TagList {
			{ "notification.type", notificationName },
			{ "notification.status", "canceled" },
			{ "error.type", oce.GetType().Name }
		};

		_notificationCounter.Add(1, tags);
		_notificationDuration.Record(durationMs, tags);
	}

	internal static void RecordNoHandlers(string notificationName) {
		var tags = new TagList {
			{ "notification.type", notificationName }
		};

		_notificationNoHandlersCounter.Add(1, tags);
	}

	#endregion

}