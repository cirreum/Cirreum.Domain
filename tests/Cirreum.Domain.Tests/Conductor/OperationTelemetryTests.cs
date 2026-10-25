namespace Cirreum.Domain.Tests.Conductor;

using Cirreum.Authorization;
using Cirreum.Conductor;
using Cirreum.Diagnostics;
using Cirreum.Domain.Tests.AuthorizationPipeline;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.Metrics;

public class OperationTelemetryTests {

	[Fact]
	public async Task Dispatch_RecordsMetrics_WithoutATraceListener() {
		var observed = new List<(string Instrument, KeyValuePair<string, object?>[] Tags)>();
		using var meterListener = new MeterListener {
			InstrumentPublished = (instrument, l) => {
				if (instrument.Meter.Name == CirreumTelemetry.Meters.ConductorDispatcher) {
					l.EnableMeasurementEvents(instrument);
				}
			}
		};
		meterListener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => Capture(instrument, tags));
		meterListener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => Capture(instrument, tags));
		meterListener.Start();

		var user = TestUserState.CreateAuthenticated(
			"admin-telemetry", "Admin User", [ApplicationRoles.AppAdminRole]);
		using var provider = AuthorizationTestHost.Build(user);
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var result = await dispatcher.DispatchAsync(new AdminOnlyCommand());

		result.IsSuccess.Should().BeTrue();
		observed.Select(o => o.Instrument).Should().Contain([
			ConductorTelemetry.OperationsTotalMetric,
			ConductorTelemetry.OperationsDurationHistogram]);
		observed.Should().AllSatisfy(o => {
			o.Instrument.Should().StartWith("cirreum.conductor.");
			o.Tags.Select(t => t.Key).Should().NotContain([
				ConductorTelemetry.OperationSucceededTag,
				ConductorTelemetry.OperationFailedTag,
				ConductorTelemetry.OperationCanceledTag]);
			o.Tags.Should().Contain(t => t.Key == ConductorTelemetry.OperationStatusTag && Equals(t.Value, "success"));
		});

		void Capture(Instrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags) {
			var copy = tags.ToArray();
			if (copy.Any(t => t.Key == ConductorTelemetry.OperationTypeTag && Equals(t.Value, nameof(AdminOnlyCommand)))) {
				lock (observed) {
					observed.Add((instrument.Name, copy));
				}
			}
		}
	}

}
