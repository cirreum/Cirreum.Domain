namespace Cirreum.Domain.Tests.AuthorizationPipeline;

/// <summary>
/// Minimal owned application user for home-membership scenarios: carries an
/// <see cref="OwnerId"/> (the caller's home company) as a pure identity fact.
/// </summary>
internal sealed record TestOwnedApplicationUser(string? OwnerId) : IOwnedApplicationUser {

	public bool IsEnabled { get; init; } = true;

	public IReadOnlyList<string> Roles { get; init; } = [];

}
