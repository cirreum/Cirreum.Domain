namespace Cirreum.Presence;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Defines a builder for configuring user presence services.
/// </summary>
/// <param name="services">
/// The <see cref="IServiceCollection"/> to configure.
/// </param>
public class UserPresenceBuilder(IServiceCollection services)
	: IUserPresenceBuilder {
	/// <summary>
	/// Gets the <see cref="IServiceCollection"/> to configure.
	/// </summary>
	public IServiceCollection Services => services;
}