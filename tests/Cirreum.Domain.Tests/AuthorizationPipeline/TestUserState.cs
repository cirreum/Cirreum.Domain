namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Security;

internal sealed class TestUserState : UserStateBase {

	public override bool IsAuthenticationComplete => true;

	private TestUserState() {
	}

	public static TestUserState CreateUnauthenticated() => new();

	/// <param name="applicationUserResolved">
	/// Marks application-user resolution as having been attempted even when
	/// <paramref name="applicationUser"/> is null — the loaded-with-null state a real
	/// accessor produces for callers whose scheme has no registered resolver. Without
	/// this, "never attempted" and "attempted, none found" are indistinguishable in tests.
	/// </param>
	public static TestUserState CreateAuthenticated(
		string id,
		string name,
		string[] roles,
		AuthenticationBoundary boundary = AuthenticationBoundary.Tenant,
		IApplicationUser? applicationUser = null,
		bool applicationUserResolved = false) {

		var identity = new ClaimsIdentity(
			authenticationType: "test",
			nameType: ClaimTypes.Name,
			roleType: ClaimTypes.Role);
		identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, id));
		identity.AddClaim(new Claim(ClaimTypes.Name, name));
		foreach (var role in roles) {
			identity.AddClaim(new Claim(ClaimTypes.Role, role));
		}

		var principal = new ClaimsPrincipal(identity);

		var state = new TestUserState {
			_principal = principal,
			_identity = identity,
			_profile = new UserProfile(principal, TimeZoneInfo.Utc.Id),
			_isAuthenticated = true
		};
		state.SetAuthenticationBoundary(boundary);
		state.StartSession();
		if (applicationUser is not null || applicationUserResolved) {
			state.SetApplicationUser(applicationUser);
		}
		return state;
	}

}
