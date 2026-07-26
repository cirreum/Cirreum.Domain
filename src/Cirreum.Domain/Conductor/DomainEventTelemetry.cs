namespace Cirreum.Conductor;

using Cirreum.Diagnostics;
using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Provides telemetry capabilities for domain-event publishing.
/// </summary>
internal static class DomainEventTelemetry {

	private static readonly ActivitySource _activitySource =
		new(CirreumTelemetry.ActivitySources.ConductorPublisher, CirreumTelemetry.Version);

	private static readonly Meter _meter =
		new(CirreumTelemetry.Meters.ConductorPublisher, CirreumTelemetry.Version);

	private static readonly Counter<long> _domainEventCounter = _meter.CreateCounter<long>(
		ConductorTelemetry.DomainEventsTotalMetric,
		description: "Total number of domain events published");

	private static readonly Counter<long> _domainEventFailedCounter = _meter.CreateCounter<long>(
		ConductorTelemetry.DomainEventsFailedTotalMetric,
		description: "Total number of domain events whose handlers failed");

	private static readonly Counter<long> _domainEventNoHandlersCounter = _meter.CreateCounter<long>(
		ConductorTelemetry.DomainEventsNoHandlersTotalMetric,
		description: "Total number of domain events published with no registered handler");

	private static readonly Histogram<double> _domainEventDuration = _meter.CreateHistogram<double>(
		ConductorTelemetry.DomainEventsDurationHistogram,
		unit: "ms",
		description: "Domain-event publishing duration in milliseconds");

	#region Activity Management

	internal static Activity? StartActivity(string domainEventName) {
		var activity = _activitySource.StartActivity(
			"Publish domainEvent",
			DomainContext.EntryPointActivityKind);

		activity?.SetTag("domainEvent.type", domainEventName);

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
			activity.SetTag("domainEvent.canceled", true);
			activity.AddException(oce);
		}
	}

	#endregion

	#region Metrics Recording

	internal static void RecordSuccess(
		string domainEventName,
		PublisherStrategy strategy,
		int handlerCount,
		double durationMs) {

		var tags = new TagList {
			{ "domainEvent.type", domainEventName },
			{ "domainEvent.strategy", strategy.ToString() },
			{ "domainEvent.handler_count", handlerCount },
			{ "domainEvent.status", "success" }
		};

		_domainEventCounter.Add(1, tags);
		_domainEventDuration.Record(durationMs, tags);
	}

	internal static void RecordFailure(
		string domainEventName,
		PublisherStrategy strategy,
		int handlerCount,
		double durationMs,
		Exception error) {

		var tags = new TagList {
			{ "domainEvent.type", domainEventName },
			{ "domainEvent.strategy", strategy.ToString() },
			{ "domainEvent.handler_count", handlerCount },
			{ "domainEvent.status", "failure" },
			{ "error.type", error.GetType().Name }
		};

		_domainEventCounter.Add(1, tags);
		_domainEventFailedCounter.Add(1, tags);
		_domainEventDuration.Record(durationMs, tags);
	}

	internal static void RecordCanceled(
		string domainEventName,
		double durationMs,
		OperationCanceledException oce) {

		var tags = new TagList {
			{ "domainEvent.type", domainEventName },
			{ "domainEvent.status", "canceled" },
			{ "error.type", oce.GetType().Name }
		};

		_domainEventCounter.Add(1, tags);
		_domainEventDuration.Record(durationMs, tags);
	}

	internal static void RecordNoHandlers(string domainEventName) {
		var tags = new TagList {
			{ "domainEvent.type", domainEventName }
		};

		_domainEventNoHandlersCounter.Add(1, tags);
	}

	#endregion

}