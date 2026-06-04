namespace Cirreum.Presence;

using Microsoft.Extensions.DependencyInjection;

public class UserPresenceBuilder(IServiceCollection services)
	: IUserPresenceBuilder {
	public IServiceCollection Services => services;
}