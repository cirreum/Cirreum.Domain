namespace Cirreum.Conductor;

using Cirreum.Diagnostics;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

/// <summary>
/// Provides telemetry capabilities for operation processing including metrics and distributed tracing.
/// </summary>
internal static class OperationTelemetry {

	private static readonly ActivitySource _activitySource =
		new(CirreumTelemetry.ActivitySources.ConductorDispatcher, CirreumTelemetry.Version);

	private static readonly Meter _meter =
		new(CirreumTelemetry.Meters.ConductorDispatcher, CirreumTelemetry.Version);

	private static readonly Counter<long> _operationCounter = _meter.CreateCounter<long>(
		ConductorTelemetry.OperationsTotalMetric,
		description: "Total number of operations dispatched");

	private static readonly Counter<long> _operationFailedCounter = _meter.CreateCounter<long>(
		ConductorTelemetry.OperationsFailedTotalMetric,
		description: "Total number of failed operations");

	private static readonly Counter<long> _operationCanceledCounter = _meter.CreateCounter<long>(
		ConductorTelemetry.OperationsCanceledTotalMetric,
		description: "Total number of canceled operations");

	private static readonly Histogram<double> _operationDuration = _meter.CreateHistogram<double>(
		ConductorTelemetry.OperationsDurationHistogram,
		unit: "ms",
		description: "Operation processing duration in milliseconds");


	// Helper to check if metrics should be recorded
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool HasListeners() {
		// Don't even create the meter if there are no listeners on the activity source
		// This is a good proxy for "is telemetry enabled?"
		return _activitySource.HasListeners();
	}

	#region Activity Management (Distributed Tracing)

	/// <summary>
	/// Starts an activity for distributed tracing. Returns null if tracing is not enabled.
	/// </summary>
	internal static Activity? StartActivity(
		string operationName,
		bool hasResponse = false,
		string? responseType = null) {

		var activity = _activitySource.StartActivity(
			"Dispatch Operation",
			DomainContext.CurrentActivityKind);

		activity?.SetTag(ConductorTelemetry.OperationTypeTag, operationName);
		activity?.SetTag(ConductorTelemetry.OperationHasResponseTag, hasResponse);

		if (hasResponse && responseType is not null) {
			activity?.SetTag(ConductorTelemetry.ResponseTypeTag, responseType);
		}

		return activity;
	}

	/// <summary>
	/// Sets success information on an activity.
	/// </summary>
	internal static void SetActivitySuccess(Activity? activity) {
		activity?.SetStatus(ActivityStatusCode.Ok);
	}

	/// <summary>
	/// Sets error information on an activity.
	/// </summary>
	internal static void SetActivityError(Activity? activity, Exception ex) {
		if (activity is not null) {
			activity.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity.SetTag(ConductorTelemetry.ErrorTypeTag, ex.GetType().Name);
			activity.SetTag(ConductorTelemetry.OperationFailedTag, true);
			activity.AddException(ex);
		}
	}

	/// <summary>
	/// Sets cancellation information on an activity.
	/// </summary>
	internal static void SetActivityCanceled(Activity? activity, OperationCanceledException oce) {
		if (activity is not null) {
			activity.SetStatus(ActivityStatusCode.Error, "Canceled");
			activity.SetTag(ConductorTelemetry.OperationCanceledTag, true);
			activity.AddException(oce);
		}
	}

	#endregion

	#region Metrics Recording

	/// <summary>
	/// Records success metrics for a completed operation.
	/// </summary>
	internal static void RecordSuccess(
		string operationTypeName,
		string? responseType,
		double durationMs) {

		RecordMetrics(
			operationTypeName,
			responseType,
			success: true,
			canceled: false,
			durationMs);
	}

	/// <summary>
	/// Records failure metrics for a failed operation.
	/// </summary>
	internal static void RecordFailure(
		string operationTypeName,
		string? responseType,
		double durationMs,
		Exception error) {

		RecordMetrics(
			operationTypeName,
			responseType,
			success: false,
			canceled: false,
			durationMs,
			error.GetType().Name);
	}

	/// <summary>
	/// Records cancellation metrics for a canceled operation.
	/// </summary>
	internal static void RecordCanceled(
		string operationTypeName,
		string? responseType,
		double durationMs,
		OperationCanceledException oce) {

		RecordMetrics(
			operationTypeName,
			responseType,
			success: false,
			canceled: true,
			durationMs,
			oce.GetType().Name);
	}

	private static void RecordMetrics(
		string operationName,
		string? responseType,
		bool success,
		bool canceled,
		double durationMs,
		string? errorType = null) {

		var tags = new TagList {
			{ ConductorTelemetry.OperationTypeTag, operationName },
			{ ConductorTelemetry.OperationSucceededTag, success },
			{ ConductorTelemetry.OperationFailedTag, !success && !canceled },
			{ ConductorTelemetry.OperationCanceledTag, canceled },
			{ ConductorTelemetry.OperationStatusTag, canceled ? "canceled" : success ? "success" : "failure" }
		};

		if (responseType is not null) {
			tags.Add(ConductorTelemetry.ResponseTypeTag, responseType);
		}

		if (errorType is not null) {
			tags.Add(ConductorTelemetry.ErrorTypeTag, errorType);
		}

		_operationCounter.Add(1, tags);
		_operationDuration.Record(durationMs, tags);

		// NOTE: by design, "canceled" is NOT counted as a failed operation
		if (!success && !canceled) {
			_operationFailedCounter.Add(1, tags);
		}

		if (canceled) {
			_operationCanceledCounter.Add(1, tags);
		}
	}

	#endregion

}