namespace Cirreum.Presence;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring user presence services on <see cref="IUserPresenceBuilder"/>.
/// </summary>
public static class UserPresenceBuilderExtensions {

	/// <summary>
	/// Registers a custom user presence service implementation with the specified refresh interval.
	/// </summary>
	/// <typeparam name="TUserPresenceService">The type of the user presence service implementation that must implement <see cref="IUserPresenceService"/>.</typeparam>
	/// <param name="builder">The <see cref="IUserPresenceBuilder"/> instance to configure.</param>
	/// <param name="refreshInterval">The interval in milliseconds at which the presence service should refresh user presence status.</param>
	/// <returns>The <see cref="IUserPresenceBuilder"/> instance for method chaining.</returns>
	/// <exception cref="InvalidOperationException">Thrown when called outside of a browser environment (not Blazor WebAssembly).</exception>
	/// <remarks>
	/// This method registers the specified presence service as a scoped service and configures the monitoring options
	/// to use the provided refresh interval. The service will be used to periodically update user presence information.
	/// <para>
	/// This method can only be called in browser environments (Blazor WebAssembly). Attempting to use it in server-side
	/// environments will result in an <see cref="InvalidOperationException"/>.
	/// </para>
	/// </remarks>
	public static IUserPresenceBuilder AddPresenceService<TUserPresenceService>(
	   this IUserPresenceBuilder builder,
	   int refreshInterval)
	   where TUserPresenceService : class, IUserPresenceService {

		if (!OperatingSystem.IsBrowser()) {
			throw new InvalidOperationException("User presence monitor is only allowed on the client.");
		}

		builder.Services.AddScoped<IUserPresenceService, TUserPresenceService>();
		builder.Services.PostConfigure<UserPresenceMonitorOptions>(o =>
			o.RefreshInterval = refreshInterval
		);

		return builder;

	}

}
