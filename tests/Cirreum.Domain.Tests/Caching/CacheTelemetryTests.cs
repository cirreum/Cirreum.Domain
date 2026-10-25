namespace Cirreum.Domain.Tests.Caching;

using Cirreum.Caching;
using Cirreum.Diagnostics;
using System.Diagnostics.Metrics;

public class CacheTelemetryTests {

	[Fact]
	public void RecordOperation_DoesNotTagTheCacheKey() {
		const string consumer = "cache-telemetry-tests";
		var observed = new List<KeyValuePair<string, object?>[]>();
		using var meterListener = new MeterListener {
			InstrumentPublished = (instrument, l) => {
				if (instrument.Meter.Name == CirreumTelemetry.Meters.ConductorCache) {
					l.EnableMeasurementEvents(instrument);
				}
			}
		};
		meterListener.SetMeasurementEventCallback<long>((_, _, tags, _) => Capture(tags));
		meterListener.SetMeasurementEventCallback<double>((_, _, tags, _) => Capture(tags));
		meterListener.Start();

		CacheTelemetry.RecordOperation("grant:v1:user-42:orders:abc", isHit: true, durationMs: 1.5, consumer);

		observed.Should().HaveCount(2);
		observed.Should().AllSatisfy(tags => {
			tags.Select(t => t.Key).Should().NotContain(CacheTelemetry.CallerTag);
			tags.Should().Contain(t => t.Key == CacheTelemetry.StatusTag && Equals(t.Value, CacheTelemetry.StatusHit));
		});

		void Capture(ReadOnlySpan<KeyValuePair<string, object?>> tags) {
			var copy = tags.ToArray();
			if (copy.Any(t => t.Key == CacheTelemetry.ConsumerTag && Equals(t.Value, consumer))) {
				lock (observed) {
					observed.Add(copy);
				}
			}
		}
	}

}
