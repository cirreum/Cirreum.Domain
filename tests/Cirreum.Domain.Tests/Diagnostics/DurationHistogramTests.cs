namespace Cirreum.Domain.Tests.Diagnostics;

using Cirreum.Caching;
using Cirreum.Conductor;
using Cirreum.Diagnostics;
using Cirreum.RemoteServices;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.Metrics;

public class DurationHistogramTests {

	private const string Probe = "duration-histogram-probe";

	private static readonly string[] DomainMeters = [
		CirreumTelemetry.Meters.ConductorDispatcher,
		CirreumTelemetry.Meters.ConductorPublisher,
		CirreumTelemetry.Meters.ConductorCache,
		CirreumTelemetry.Meters.RemoteServicesClient];

	private sealed record Measurement(Instrument Instrument, double Value, KeyValuePair<string, object?>[] Tags);

	/// <summary>Captures measurements from Domain's meters that carry the probe value in any tag.</summary>
	private static (MeterListener Listener, List<Measurement> Measurements) Listen() {
		var measurements = new List<Measurement>();
		var listener = new MeterListener {
			InstrumentPublished = (instrument, l) => {
				if (DomainMeters.Contains(instrument.Meter.Name)) {
					l.EnableMeasurementEvents(instrument);
				}
			}
		};
		listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Capture(instrument, value, tags));
		listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Capture(instrument, value, tags));
		listener.Start();
		return (listener, measurements);

		void Capture(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags) {
			var copy = tags.ToArray();
			if (copy.Any(t => Equals(t.Value, Probe))) {
				lock (measurements) {
					measurements.Add(new(instrument, value, copy));
				}
			}
		}
	}

	[Fact]
	public void EveryDurationHistogram_RecordsSecondsWithTheSharedBuckets() {
		var (listener, measurements) = Listen();
		using (listener) {
			OperationTelemetry.RecordSuccess(Probe, responseType: null, durationMs: 250);
			DomainEventTelemetry.RecordSuccess(Probe, PublisherStrategy.Sequential, handlerCount: 1, durationMs: 250);
			RemoteClientTelemetry.RecordSuccess("GET", "api/x", Probe, 200, durationMs: 250, NullLogger.Instance);
			CacheTelemetry.RecordOperation("key", isHit: true, durationMs: 250, consumer: Probe);
		}

		var durations = measurements.Where(m => m.Instrument is Histogram<double>).ToList();
		durations.Select(m => m.Instrument.Name).Should().BeEquivalentTo([
			"cirreum.conductor.operation.duration",
			"cirreum.conductor.domain_event.duration",
			"cirreum.remote_services.client.request.duration",
			"cirreum.cache.operation.duration"]);
		durations.Should().AllSatisfy(m => {
			m.Instrument.Unit.Should().Be("s");
			m.Value.Should().Be(0.25);
			((Histogram<double>)m.Instrument).Advice!.HistogramBucketBoundaries
				.Should().Equal(CirreumTelemetry.Histograms.DurationAdvice.HistogramBucketBoundaries);
		});
	}

	[Fact]
	public void ErrorType_IsTheFullyQualifiedExceptionName() {
		var (listener, measurements) = Listen();
		using (listener) {
			OperationTelemetry.RecordFailure(Probe, responseType: null, durationMs: 1, new InvalidOperationException());
		}

		measurements.Should().NotBeEmpty().And.AllSatisfy(m =>
			m.Tags.Should().Contain(t => t.Key == ConductorTelemetry.ErrorTypeTag
				&& Equals(t.Value, "System.InvalidOperationException")));
	}

}
