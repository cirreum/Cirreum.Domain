namespace Cirreum.Domain.Tests.RemoteServices;

using Cirreum.Diagnostics;
using Cirreum.RemoteServices;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;

public class RemoteClientTelemetryTests {

	/// <summary>Test double exposing a protected verb.</summary>
	private sealed class TestClient(HttpClient client)
		: RemoteClient(client, NullLogger.Instance, new TestEnvironment(), activitySource: null) {

		public Task<Result> Get(string endpoint) => this.GetAsync(endpoint);

	}

	private sealed class TestEnvironment : IDomainEnvironment {
		public string ApplicationName => "test";
		public string EnvironmentName => "Development";
		public DomainRuntimeType RuntimeType => DomainRuntimeType.WebApi;
	}

	/// <summary>Records <see cref="Activity.Current"/> at the moment the request is sent.</summary>
	private sealed class CapturingHandler : HttpMessageHandler {

		public Activity? ActivityAtSend { get; private set; }

		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken) {
			this.ActivityAtSend = Activity.Current;
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
		}

	}

	private static TestClient CreateClient(CapturingHandler handler) =>
		new(new HttpClient(handler) { BaseAddress = new Uri("https://remote.test/") });

	[Fact]
	public async Task Request_IsSentInsideTheRemoteClientSpan() {
		using var listener = new ActivityListener {
			ShouldListenTo = source => source.Name == CirreumTelemetry.ActivitySources.RemoteServicesClient,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
		};
		ActivitySource.AddActivityListener(listener);
		var handler = new CapturingHandler();

		var result = await CreateClient(handler).Get("api/orders/1001");

		result.IsSuccess.Should().BeTrue();
		var span = FindRemoteClientSpan(handler.ActivityAtSend);
		span.Should().NotBeNull();
		span!.DisplayName.Should().Be("HTTP GET");
		span.Kind.Should().Be(ActivityKind.Client);
	}

	[Fact]
	public async Task RequestMetrics_AreNotTaggedWithThePath() {
		var observed = new List<(string Instrument, KeyValuePair<string, object?>[] Tags)>();
		using var meterListener = new MeterListener {
			InstrumentPublished = (instrument, l) => {
				if (instrument.Meter.Name == CirreumTelemetry.Meters.RemoteServicesClient) {
					l.EnableMeasurementEvents(instrument);
				}
			}
		};
		meterListener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => Capture(instrument, tags));
		meterListener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => Capture(instrument, tags));
		meterListener.Start();

		await CreateClient(new CapturingHandler()).Get("api/orders/1001");

		observed.Select(o => o.Instrument).Should().Contain([
			"cirreum.remote_services.client.requests",
			"cirreum.remote_services.client.request.duration"]);
		observed.Should().AllSatisfy(o => {
			o.Tags.Select(t => t.Key).Should().NotContain([
				"url.path",
				"remote_services.request.succeeded",
				"remote_services.request.failed",
				"remote_services.request.canceled"]);
			o.Tags.Should().Contain(t => t.Key == "http.request.method" && Equals(t.Value, "GET"));
		});

		void Capture(Instrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags) {
			var copy = tags.ToArray();
			if (copy.Any(t => t.Key == "remote_services.client.type" && Equals(t.Value, nameof(TestClient)))) {
				lock (observed) {
					observed.Add((instrument.Name, copy));
				}
			}
		}
	}

	private static Activity? FindRemoteClientSpan(Activity? activity) {
		for (var current = activity; current is not null; current = current.Parent) {
			if (current.Source.Name == CirreumTelemetry.ActivitySources.RemoteServicesClient) {
				return current;
			}
		}
		return null;
	}

}
