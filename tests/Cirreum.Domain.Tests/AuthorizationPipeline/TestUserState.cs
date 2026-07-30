namespace Cirreum.Domain.Tests.AuthorizationPipeline;

using Cirreum.Security;

internal sealed class TestUserState : UserStateBase {

	public override bool IsAuthenticationComplete => true;

	private TestUserState() {
	}

	public static TestUserState CreateUnauthenticated() => new();

	public static TestUserState CreateAuthenticated(
		string id,
		string name,
		string[] roles,
		AuthenticationBoundary boundary = AuthenticationBoundary.Tenant) {

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
		return state;
	}

}
