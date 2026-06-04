namespace Cirreum.Caching;

using Cirreum.Diagnostics;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

/// <summary>
/// Centralized telemetry for the Cirreum cache layer. Publishes a shared
/// <see cref="Meter"/> and stable tag-name constants used by the
/// <see cref="InstrumentedCacheService"/> decorator.
/// </summary>
/// <remarks>
/// <para>
/// Cache operations are already children of Conductor or Authorization
/// activities, so no dedicated <see cref="ActivitySource"/>
/// is needed — only metrics.
/// </para>
/// </remarks>
public static class CacheTelemetry {

	// Status values ——————————————————————————————————————————

	/// <summary>Status tag value: cache hit (value served from cache).</summary>
	public const string StatusHit = "hit";

	/// <summary>Status tag value: cache miss (factory executed).</summary>
	public const string StatusMiss = "miss";

	// Tag names ——————————————————————————————————————————————

	/// <summary>Tag: cache operation status (<see cref="StatusHit"/> / <see cref="StatusMiss"/>).</summary>
	public const string StatusTag = "cirreum.cache.status";

	/// <summary>Tag: cache key identifying the operation.</summary>
	public const string CallerTag = "cirreum.cache.caller";

	/// <summary>Tag: subsystem that triggered the cache operation (e.g. "query-caching", "grant-resolution").</summary>
	public const string ConsumerTag = "cirreum.cache.consumer";

	// Metric names ———————————————————————————————————————————

	/// <summary>Metric: cache operation duration in milliseconds.</summary>
	public const string DurationMetric = "cirreum.cache.duration";

	/// <summary>Metric: total cache operations (tagged with hit/miss).</summary>
	public const string OperationsMetric = "cirreum.cache.operations";

	// Meter + instruments ————————————————————————————————————

	private static readonly Meter _meter =
		new(CirreumTelemetry.Meters.ConductorCache, CirreumTelemetry.Version);

	private static readonly Histogram<double> _durationHistogram = _meter.CreateHistogram<double>(
		DurationMetric,
		unit: "ms",
		description: "Cache operation duration in milliseconds");

	private static readonly Counter<long> _operationsCounter = _meter.CreateCounter<long>(
		OperationsMetric,
		description: "Total number of cache operations");

	// Recording ——————————————————————————————————————————————

	/// <summary>
	/// Records a cache operation (hit or miss) with its duration.
	/// </summary>
	/// <param name="caller">Cache key identifying the operation.</param>
	/// <param name="isHit"><see langword="true"/> if the value was served from cache; <see langword="false"/> if the factory executed.</param>
	/// <param name="durationMs">Operation duration in milliseconds.</param>
	/// <param name="consumer">Subsystem that triggered the operation (e.g. "query-caching", "grant-resolution").</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void RecordOperation(string caller, bool isHit, double durationMs, string consumer) {
		var status = isHit ? StatusHit : StatusMiss;

		var tags = new TagList {
			{ StatusTag, status },
			{ CallerTag, caller },
			{ ConsumerTag, consumer }
		};

		_operationsCounter.Add(1, tags);
		_durationHistogram.Record(durationMs, tags);
	}
}
