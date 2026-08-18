namespace Cirreum.Presence;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring user presence services on
/// <see cref="IUserPresenceBuilder"/>.
/// </summary>
public static class UserPresenceBuilderExtensions {

	/// <summary>
	/// Registers a custom user presence service for client-side presence monitoring.
	/// </summary>
	/// <typeparam name="TUserPresenceService">
	/// The <see cref="IUserPresenceService"/> implementation to register.
	/// </typeparam>
	/// <param name="builder">
	/// The <see cref="IUserPresenceBuilder"/> to configure.
	/// </param>
	/// <param name="refreshInterval">
	/// An optional interval, in milliseconds, between presence refreshes. When
	/// <see langword="null"/>, the existing
	/// <see cref="UserPresenceMonitorOptions.RefreshInterval"/> configuration is
	/// preserved. A value of <c>0</c> disables the monitor. Positive values must be
	/// greater than <see cref="UserPresenceMonitorOptions.MinimumRefreshInterval"/>.
	/// </param>
	/// <returns>
	/// The configured <see cref="IUserPresenceBuilder"/> for further chaining.
	/// </returns>
	/// <remarks>
	/// <para>
	/// Registers <typeparamref name="TUserPresenceService"/> as the scoped
	/// <see cref="IUserPresenceService"/>.
	/// </para>
	/// <para>
	/// When <paramref name="refreshInterval"/> is specified, it is applied through
	/// post-configuration and therefore overrides earlier configuration of
	/// <see cref="UserPresenceMonitorOptions.RefreshInterval"/>. When omitted, this
	/// method does not modify the configured refresh interval.
	/// </para>
	/// <para>
	/// Presence monitoring is supported only in browser environments, such as
	/// Blazor WebAssembly.
	/// </para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">
	/// Thrown when presence monitoring is configured outside a browser environment.
	/// </exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="refreshInterval"/> is negative or is between <c>1</c> and
	/// <c>5000</c> milliseconds, inclusive.
	/// </exception>
	public static IUserPresenceBuilder AddPresenceService<TUserPresenceService>(
		this IUserPresenceBuilder builder,
		int? refreshInterval = null)
		where TUserPresenceService : class, IUserPresenceService {

		if (!OperatingSystem.IsBrowser()) {
			throw new InvalidOperationException(
				"User presence monitoring is only supported in a browser environment.");
		}

		if (refreshInterval is < 0 ||
			refreshInterval is > 0 and <= UserPresenceMonitorOptions.MinimumRefreshInterval) {

			throw new ArgumentOutOfRangeException(
				nameof(refreshInterval),
				refreshInterval,
				"Presence refresh interval must be greater than 5 seconds, or 0 to disable monitoring.");
		}

		builder.Services.AddScoped<IUserPresenceService, TUserPresenceService>();

		if (refreshInterval is not null) {
			builder.Services.PostConfigure<UserPresenceMonitorOptions>(options =>
				options.RefreshInterval = refreshInterval.Value
			);
		}

		return builder;
	}

}