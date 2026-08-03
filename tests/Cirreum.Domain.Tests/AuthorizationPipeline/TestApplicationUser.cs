namespace Cirreum.Domain.Tests.AuthorizationPipeline;

/// <summary>
/// Minimal application user that does NOT implement <see cref="IOwnedApplicationUser"/> —
/// the shape an app uses when it has no tenant/company dimension. Exists to prove the
/// disabled-user gate reads <see cref="IsEnabled"/> from any application user, not only
/// from owned ones.
/// </summary>
internal sealed record TestApplicationUser : IApplicationUser {

	public bool IsEnabled { get; init; } = true;

	public IReadOnlyList<string> Roles { get; init; } = [];
}
