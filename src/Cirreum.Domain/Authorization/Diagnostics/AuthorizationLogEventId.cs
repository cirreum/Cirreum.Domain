namespace Cirreum.Authorization.Diagnostics;

internal static class AuthorizationLogEventId {

	public const int BeginAuthorizingId = 10_001;
	public const int AuthorizingDeniedId = 10_002;
	public const int AuthorizingDeniedById = 10_003;
	public const int AuthorizingAllowedId = 10_004;
	public const int AuthorizingUnknownErrorId = 10_005;
}
