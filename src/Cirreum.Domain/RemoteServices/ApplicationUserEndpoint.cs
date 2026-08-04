namespace Cirreum.RemoteServices;

/// <summary>
/// The framework-owned application-user bootstrap endpoint, shared by the server host
/// (which maps it) and the WebAssembly client (which calls it) so the two ends cannot drift.
/// </summary>
/// <remarks>
/// <para>
/// The endpoint returns the caller's own application user, read from server-resolved user
/// state. It requires authentication and nothing else — it is never dispatched through the
/// operation pipeline, so no authorization gate stands between a disabled caller and the
/// record describing their state. This is what lets a client render a "your account is
/// disabled" experience: the record whose <c>IsEnabled</c> it needs cannot itself be behind
/// the disabled gate.
/// </para>
/// <para>
/// The <c>/_cirreum/</c> prefix is the framework's reserved route namespace; application
/// routes cannot collide with it, and future framework endpoints become siblings under it.
/// </para>
/// </remarks>
public static class ApplicationUserEndpoint {

	/// <summary>
	/// The route the server maps and the client calls: <c>/_cirreum/application-user</c>.
	/// </summary>
	public const string Route = "/_cirreum/application-user";

}
