namespace Cirreum;

using Cirreum.Security;
using System.Security.Claims;
using System.Text.Json;

/// <summary>
/// Enriches the standard <see cref="UserProfile"/> from claims in a <see cref="ClaimsIdentity"/> instance.
/// Core identity properties (Id, Name, Roles, Provider, Oid, PreferredUserName, OrganizationId) are already
/// resolved by <see cref="UserProfile(ClaimsPrincipal, string)"/> via <see cref="ClaimsHelper"/> and are
/// not duplicated here.
/// </summary>
public class ClaimsUserProfileEnricher : IUserProfileEnricher {

	private static readonly JsonSerializerOptions _addressJsonOptions = new() {
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
	};

	/// <inheritdoc/>
	public Task EnrichProfileAsync(UserProfile profile, ClaimsIdentity identity) {
		EnrichProfile(profile, identity, false);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Enriches the profile with additional claims beyond the core identity properties.
	/// </summary>
	/// <param name="profile">The <see cref="UserProfile"/> being enriched.</param>
	/// <param name="identity">The <see cref="ClaimsIdentity"/> providing the claims.</param>
	/// <param name="captureUnknownClaims">When true, stores unrecognized claims in <see cref="UserProfile.AdditionalData"/>.</param>
	public static void EnrichProfile(UserProfile profile, ClaimsIdentity identity, bool captureUnknownClaims) {
		ArgumentNullException.ThrowIfNull(profile);
		ArgumentNullException.ThrowIfNull(identity);

		ProcessProfileClaims(profile, identity);
		ProcessOrganizationName(profile, identity);
		ProcessDirectoryGroups(profile, identity);
		ProcessAddressClaim(profile, identity);

		if (captureUnknownClaims) {
			CaptureUnknownClaims(profile, identity);
		}
	}

	private static void ProcessProfileClaims(UserProfile profile, ClaimsIdentity identity) {
		foreach (var claim in identity.Claims) {
			switch (claim.Type.ToLowerInvariant()) {

				// Core Profile Claims
				case "given_name":
					profile.GivenName = claim.Value;
					break;
				case "family_name":
					profile.FamilyName = claim.Value;
					break;
				case "middle_name":
					profile.MiddleName = claim.Value;
					break;
				case "nickname":
					profile.Nickname = claim.Value;
					break;
				case "picture":
					profile.Picture = claim.Value.NullIfWhiteSpace() ?? "/assets/images/guest-user-icon.svg";
					break;
				case "locale":
					profile.Locale = claim.Value;
					break;
				case "zoneinfo":
					profile.TimeZone = claim.Value;
					break;
				case "birthdate":
					profile.Birthdate = claim.Value;
					break;
				case "updated_at":
					if (claim.Value.HasValue() && DateTimeOffset.TryParse(claim.Value, out var updatedAt)) {
						profile.UpdatedAt = updatedAt;
					}
					break;

				// Email Claims
				case "email":
					profile.Email = claim.Value;
					break;
				case "email_verified":
					profile.EmailVerified = bool.TryParse(claim.Value, out var emailVerified) ? emailVerified : null;
					break;

				// Phone Claims
				case "phone_number":
					profile.PhoneNumber = claim.Value;
					break;
				case "phone_number_verified":
					profile.PhoneNumberVerified = bool.TryParse(claim.Value, out var phoneVerified) ? phoneVerified : null;
					break;
			}
		}
	}

	private static void ProcessOrganizationName(UserProfile profile, ClaimsIdentity identity) {
		var orgName = identity.FindFirst("org_name")?.Value
					  ?? identity.FindFirst("org")?.Value
					  ?? identity.FindFirst("tenant_name")?.Value;

		if (orgName.HasValue()) {
			profile.Organization = profile.Organization with {
				OrganizationName = orgName
			};
		}
	}

	private static void ProcessDirectoryGroups(UserProfile profile, ClaimsIdentity identity) {
		foreach (var claim in identity.Claims.Where(c => c.Type is "groups")) {
			List<string> groupValues;
			try {
				groupValues = JsonSerializer.Deserialize<List<string>>(claim.Value) ?? [];
			} catch (JsonException) {
				groupValues = [.. claim.Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
			}

			foreach (var group in groupValues) {
				profile.Organization.DirectoryGroupsRaw.Add(new UserProfileMembership(
					group,
					group,
					UserProfileMembershipType.DirectoryGroup
				));
			}
		}
	}

	private static void ProcessAddressClaim(UserProfile profile, ClaimsIdentity identity) {
		var addressClaim = identity.FindFirst("address")?.Value;
		if (string.IsNullOrEmpty(addressClaim)) {
			return;
		}

		try {
			var address = JsonSerializer.Deserialize<ClaimsAddress>(addressClaim, _addressJsonOptions);
			if (address is not null) {
				profile.Address = new UserProfileAddress {
					City = address.Locality,
					Country = address.Country,
					PostalCode = address.PostalCode,
					State = address.Region,
					StreetAddress = address.StreetAddress
				};
			}
		} catch (JsonException ex) {
			System.Diagnostics.Debug.WriteLine($"Failed to deserialize address claim: {ex.Message}");
		}
	}

	private static readonly HashSet<string> _knownClaimTypes = new(StringComparer.OrdinalIgnoreCase) {
		// Core identity (handled by UserProfile constructor via ClaimsHelper)
		"sub", "oid", "name", "preferred_username", "roles", "role",
		"tid", "org_id", "iss",
		// Profile claims (handled by ProcessProfileClaims)
		"given_name", "family_name", "middle_name", "nickname", "picture",
		"locale", "zoneinfo", "birthdate", "updated_at",
		"email", "email_verified", "phone_number", "phone_number_verified",
		// Organization (handled by ProcessOrganizationName / ProcessDirectoryGroups)
		"org", "org_name", "tenant_name", "groups",
		// Address (handled by ProcessAddressClaim)
		"address"
	};

	private static void CaptureUnknownClaims(UserProfile profile, ClaimsIdentity identity) {
		foreach (var claim in identity.Claims) {
			if (!_knownClaimTypes.Contains(claim.Type)) {
				profile.AdditionalData[claim.Type] = claim.Value;
			}
		}
	}

	private class ClaimsAddress {
		public string? Formatted { get; set; }
		public string? StreetAddress { get; set; }
		public string? Locality { get; set; }
		public string? Region { get; set; }
		public string? PostalCode { get; set; }
		public string? Country { get; set; }
	}

}
