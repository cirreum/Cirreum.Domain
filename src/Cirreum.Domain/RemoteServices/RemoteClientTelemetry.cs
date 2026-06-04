namespace Cirreum.RemoteServices;

using Cirreum.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
/// Provides telemetry capabilities for remote client operations including metrics and distributed tracing.
/// Works in all environments: WASM, MAUI, Server, Serverless.
/// </summary>
internal static partial class RemoteClientTelemetry {

	private static readonly ActivitySource _activitySource =
		new(CirreumTelemetry.ActivitySources.RemoteServicesClient, CirreumTelemetry.Version);

	private static readonly Meter _meter =
		new(CirreumTelemetry.Meters.RemoteServicesClient, CirreumTelemetry.Version);

	// Counters
	private static readonly Counter<long> _requestCounter = _meter.CreateCounter<long>(
		"remote_services.client.requests",
		description: "Total number of remote client requests");

	private static readonly Counter<long> _requestFailedCounter = _meter.CreateCounter<long>(
		"remote_services.client.requests.failed",
		description: "Total number of failed remote client requests");

	private static readonly Counter<long> _requestCanceledCounter = _meter.CreateCounter<long>(
		"remote_services.client.requests.canceled",
		description: "Total number of canceled remote client requests");

	private static readonly Histogram<double> _requestDuration = _meter.CreateHistogram<double>(
		"remote_services.client.request.duration",
		unit: "ms",
		description: "Remote client request duration in milliseconds");

	#region Activity Management (Distributed Tracing)

	internal static Activity? StartActivity(
		string httpMethod,
		string endpoint,
		string clientType) {
		var activity = _activitySource.StartActivity(
			$"HTTP {httpMethod}",
			ActivityKind.Client);

		activity?.SetTag("http.request.method", httpMethod);
		activity?.SetTag("url.path", endpoint);
		activity?.SetTag("remote_services.client.type", clientType);

		return activity;
	}

	internal static void SetActivitySuccess(Activity? activity, int statusCode) {
		activity?.SetTag("http.response.status_code", statusCode);
		activity?.SetStatus(ActivityStatusCode.Ok);
	}

	internal static void SetActivityError(Activity? activity, Exception ex, int? statusCode = null) {
		if (activity is not null) {
			activity.SetStatus(ActivityStatusCode.Error, ex.Message);
			activity.SetTag("error.type", ex.GetType().Name);
			activity.SetTag("remote_services.request.failed", true);

			if (statusCode.HasValue) {
				activity.SetTag("http.response.status_code", statusCode.Value);
			}

			activity.AddException(ex);
		}
	}

	internal static void SetActivityCanceled(Activity? activity, OperationCanceledException oce) {
		if (activity is not null) {
			activity.SetStatus(ActivityStatusCode.Error, "Canceled");
			activity.SetTag("remote_services.request.canceled", true);
			activity.AddException(oce);
		}
	}

	#endregion

	#region Metrics Recording

	internal static void RecordSuccess(
		string httpMethod,
		string endpoint,
		string clientType,
		int statusCode,
		double durationMs,
		ILogger logger) {
		RecordMetrics(
			httpMethod,
			endpoint,
			clientType,
			statusCode,
			success: true,
			canceled: false,
			durationMs);

		logger.LogRequestCompleted(httpMethod, endpoint, durationMs, statusCode);
	}

	internal static void RecordFailure(
		string httpMethod,
		string endpoint,
		string clientType,
		int? statusCode,
		double durationMs,
		Exception error,
		ILogger logger) {
		RecordMetrics(
			httpMethod,
			endpoint,
			clientType,
			statusCode,
			success: false,
			canceled: false,
			durationMs,
			error.GetType().Name);

		logger.LogRequestFailed(error, httpMethod, endpoint, durationMs, statusCode);
	}

	internal static void RecordCanceled(
		string httpMethod,
		string endpoint,
		string clientType,
		double durationMs,
		OperationCanceledException oce,
		ILogger logger) {
		RecordMetrics(
			httpMethod,
			endpoint,
			clientType,
			statusCode: null,
			success: false,
			canceled: true,
			durationMs,
			oce.GetType().Name);

		logger.LogRequestCanceled(httpMethod, endpoint, durationMs);
	}

	private static void RecordMetrics(
		string httpMethod,
		string endpoint,
		string clientType,
		int? statusCode,
		bool success,
		bool canceled,
		double durationMs,
		string? errorType = null) {
		var tags = new TagList
		{
			{ "http.request.method", httpMethod },
			{ "url.path", endpoint },
			{ "remote_services.client.type", clientType },
			{ "remote_services.request.succeeded", success },
			{ "remote_services.request.failed", !success && !canceled },
			{ "remote_services.request.canceled", canceled },
			{ "remote_services.request.status", canceled ? "canceled" : success ? "success" : "failure" }
		};

		if (statusCode.HasValue) {
			tags.Add("http.response.status_code", statusCode.Value);
		}

		if (errorType is not null) {
			tags.Add("error.type", errorType);
		}

		_requestCounter.Add(1, tags);
		_requestDuration.Record(durationMs, tags);

		if (!success && !canceled) {
			_requestFailedCounter.Add(1, tags);
		}

		if (canceled) {
			_requestCanceledCounter.Add(1, tags);
		}
	}

	#endregion

}