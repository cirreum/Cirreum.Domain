namespace Cirreum.Domain.Tests.RemoteServices;

using System.Net;
using System.Text;

/// <summary>Answers every request with the response produced by <c>respond</c>.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler {

	public int Calls { get; private set; }

	public static StubHttpHandler Returning(HttpStatusCode status, string? body = null, string mediaType = "application/json") =>
		new(_ => new HttpResponseMessage(status) {
			Content = body is null
				? new StringContent(string.Empty)
				: new StringContent(body, Encoding.UTF8, mediaType)
		});

	protected override Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken) {
		this.Calls++;
		cancellationToken.ThrowIfCancellationRequested();
		return Task.FromResult(respond(request));
	}

}
