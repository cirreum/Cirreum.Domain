namespace Cirreum.Domain.Tests.ProfileEnrichment;

public class ClaimsUserProfileEnricherTests {

	private static UserProfile ProfileFor(params Claim[] claims) {
		var identity = new ClaimsIdentity(claims, authenticationType: "test", nameType: "name", roleType: "roles");
		var profile = new UserProfile(new ClaimsPrincipal(identity), TimeZoneInfo.Utc.Id);
		ClaimsUserProfileEnricher.EnrichProfile(profile, identity, captureUnknownClaims: false);
		return profile;
	}

	// -------------------------------------------------------------------------
	// DisplayName consolidation
	// -------------------------------------------------------------------------

	[Fact]
	public void DisplayName_prefers_the_nickname_claim_over_the_name_claim() {
		// UserProfile.Name is already resolved from the name claim at construction, so
		// DisplayName favors the more casual, more differentiated nickname claim first —
		// falling back to name would just duplicate Name.
		var profile = ProfileFor(
			new Claim("name", "Glen Banta"),
			new Claim("nickname", "Gilly"),
			new Claim("given_name", "Glen"),
			new Claim("family_name", "Banta"));

		profile.DisplayName.Should().Be("Gilly");
	}

	[Fact]
	public void DisplayName_falls_back_to_the_name_claim_when_there_is_no_nickname_claim() {
		var profile = ProfileFor(
			new Claim("name", "Glen Banta"),
			new Claim("given_name", "Glen"),
			new Claim("family_name", "Banta"));

		profile.DisplayName.Should().Be("Glen Banta");
	}

	[Fact]
	public void DisplayName_falls_back_to_a_given_and_family_name_composite() {
		var profile = ProfileFor(
			new Claim("given_name", "Glen"),
			new Claim("family_name", "Banta"));

		profile.DisplayName.Should().Be("Glen Banta");
	}

	[Fact]
	public void DisplayName_composite_uses_whichever_name_part_is_present() {
		var profile = ProfileFor(new Claim("given_name", "Glen"));

		profile.DisplayName.Should().Be("Glen");
	}

	[Fact]
	public void A_blank_name_part_is_excluded_from_the_composite_rather_than_joined_as_whitespace() {
		var profile = ProfileFor(
			new Claim("given_name", "  "),
			new Claim("family_name", "Banta"));

		profile.DisplayName.Should().Be("Banta");
	}

	[Fact]
	public void The_composite_is_null_when_both_name_parts_are_blank() {
		var profile = ProfileFor(
			new Claim("given_name", "  "),
			new Claim("family_name", " "));

		profile.DisplayName.Should().BeNull();
	}

	[Fact]
	public void DisplayName_is_null_when_no_name_bearing_claim_is_present() {
		var profile = ProfileFor(new Claim("email", "glen@example.com"));

		profile.DisplayName.Should().BeNull();
	}

	[Fact]
	public void DisplayName_is_fill_only_and_never_overwrites_a_value_already_set() {
		// Simulates a richer enrichment (e.g. Microsoft Graph) that already populated
		// DisplayName before the claims pass runs, or ran and set it beforehand — the claims
		// consolidation must never clobber it, regardless of ordering.
		var identity = new ClaimsIdentity(
			[new Claim("name", "Claims Name")],
			authenticationType: "test", nameType: "name", roleType: "roles");
		var profile = new UserProfile(new ClaimsPrincipal(identity), TimeZoneInfo.Utc.Id) {
			DisplayName = "Directory Display Name"
		};

		ClaimsUserProfileEnricher.EnrichProfile(profile, identity, captureUnknownClaims: false);

		profile.DisplayName.Should().Be("Directory Display Name");
	}

	[Fact]
	public void A_blank_nickname_claim_falls_through_to_the_name_claim() {
		var profile = ProfileFor(
			new Claim("nickname", "  "),
			new Claim("name", "Glen Banta"));

		profile.DisplayName.Should().Be("Glen Banta");
	}

	[Fact]
	public void A_blank_name_claim_falls_through_to_the_composite_when_there_is_no_nickname() {
		var profile = ProfileFor(
			new Claim("name", "  "),
			new Claim("given_name", "Glen"),
			new Claim("family_name", "Banta"));

		profile.DisplayName.Should().Be("Glen Banta");
	}

	[Fact]
	public void DisplayName_resolves_a_name_claim_type_the_provider_renamed() {
		// A provisioned customName is aliased to the identity's configured NameClaimType, not to
		// the literal "name" — so resolving only "name" here would drop a provisioned name and
		// silently hand the slot to the composite. The composite is deliberately a different
		// string than the name claim, so a regression cannot pass by coincidence.
		var identity = new ClaimsIdentity(
			[
				new Claim("appName", "Glen T. Banta"),
				new Claim("given_name", "Glen"),
				new Claim("family_name", "Banta")
			],
			authenticationType: "test", nameType: "appName", roleType: "roles");
		var profile = new UserProfile(new ClaimsPrincipal(identity), TimeZoneInfo.Utc.Id);

		ClaimsUserProfileEnricher.EnrichProfile(profile, identity, captureUnknownClaims: false);

		profile.DisplayName.Should().Be("Glen T. Banta");
	}

	// -------------------------------------------------------------------------
	// Pre-existing claim mapping (first coverage — smoke tests only, not exhaustive)
	// -------------------------------------------------------------------------

	[Fact]
	public void The_last_claim_wins_when_a_profile_claim_appears_more_than_once() {
		// Last-wins is intentional, not incidental. A canonicalized custom* alias is appended
		// after the token's native claims, so an application-minted value overrides the identity
		// provider's — the app minted it because its own store is the authority.
		var profile = ProfileFor(
			new Claim("given_name", "glen"),
			new Claim("given_name", "Glen"));

		profile.GivenName.Should().Be("Glen");
	}

	[Fact]
	public void The_nickname_claim_maps_to_Nickname() {
		var profile = ProfileFor(new Claim("nickname", "Gilly"));

		profile.Nickname.Should().Be("Gilly");
	}

	[Fact]
	public void Given_and_family_name_claims_map_independently() {
		var profile = ProfileFor(
			new Claim("given_name", "Glen"),
			new Claim("family_name", "Banta"));

		profile.GivenName.Should().Be("Glen");
		profile.FamilyName.Should().Be("Banta");
	}
}
