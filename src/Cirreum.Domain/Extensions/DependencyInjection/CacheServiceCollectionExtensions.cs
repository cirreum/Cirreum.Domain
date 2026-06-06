namespace Cirreum;

using Cirreum.Caching;
using Cirreum.Caching.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Linq;

/// <summary>
/// Extension methods for registering Cirreum's centralized cache infrastructure.
/// </summary>
/// <remarks>
/// Provider selection is <em>code-first</em>: <see cref="AddCirreumCaching"/> registers the settings and a
/// no-op default; call <see cref="AddInMemoryCacheService"/> (or an infrastructure package's
/// <c>Add*CacheService</c>) to choose a provider. Each provider registration <em>replaces</em> the active
/// <see cref="ICacheService"/> via <see cref="AddCacheService"/>, so it works in any order after
/// <c>AddCirreumCaching</c> / <c>AddDomainServices</c>.
/// </remarks>
public static class CacheServiceCollectionExtensions {

	/// <summary>
	/// Registers the centralized <see cref="CacheSettings"/>, the scoped <see cref="CacheKeyContext"/>, and a
	/// no-op default <see cref="ICacheService"/> (caching disabled). Opt into a provider with
	/// <see cref="AddInMemoryCacheService"/> or an infrastructure package's <c>Add*CacheService</c>.
	/// Idempotent — safe to call from multiple subsystem registrations.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to register with.</param>
	/// <param name="configuration">
	/// Optional <see cref="IConfiguration"/> used to bind settings from the <c>Cirreum:Cache</c> section.
	/// </param>
	/// <param name="configureCaching">
	/// Optional delegate to override cache settings beyond what configuration provides. Applied after binding.
	/// </param>
	public static IServiceCollection AddCirreumCaching(
		this IServiceCollection services,
		IConfiguration? configuration = null,
		Action<CacheSettings>? configureCaching = null) {

		ArgumentNullException.ThrowIfNull(services);

		// Idempotent — only register the base once.
		if (services.Any(sd => sd.ServiceType == typeof(CacheSettings))) {
			return services;
		}

		var settings = new CacheSettings();
		if (configuration is not null) {
			configuration.GetSection(CacheSettings.SectionPath).Bind(settings);
		}

		configureCaching?.Invoke(settings);
		services.AddSingleton(settings);

		// Scoped cache key context for upstream pipeline stages (e.g. grant evaluation) to stamp key
		// prefixes and extra tags consumed by QueryCaching.
		services.TryAddScoped<CacheKeyContext>();

		// Default: no caching (safe). A provider's Add*CacheService call replaces these via AddCacheService.
		services.TryAddSingleton<ICacheService, NoCacheService>();
		services.TryAddKeyedSingleton<ICacheService>(CacheConsumers.QueryCaching, static (_, _) => new NoCacheService());
		services.TryAddKeyedSingleton<ICacheService>(CacheConsumers.GrantResolution, static (_, _) => new NoCacheService());

		return services;
	}

	/// <summary>
	/// Selects the single-instance in-memory cache as the active <see cref="ICacheService"/>
	/// (replacing the no-op default). Suitable for Blazor WASM, development, testing, and single-instance hosts.
	/// </summary>
	public static IServiceCollection AddInMemoryCacheService(this IServiceCollection services) =>
		services.AddCacheService(static _ => new InMemoryCacheService());

	/// <summary>
	/// Sets <paramref name="implementationFactory"/> as the active <see cref="ICacheService"/> — wrapping it
	/// in the telemetry decorator and registering the per-consumer keyed instances
	/// (<see cref="CacheConsumers"/>). <em>Replaces</em> any previously registered cache service (the no-op
	/// default or a prior provider), so it is safe to call after <c>AddCirreumCaching</c> /
	/// <c>AddDomainServices</c> regardless of order. Intended for infrastructure cache-provider packages.
	/// </summary>
	public static IServiceCollection AddCacheService(
		this IServiceCollection services,
		Func<IServiceProvider, ICacheService> implementationFactory) {

		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(implementationFactory);

		RemoveCacheServiceRegistrations(services);

		// Raw (undecorated) implementation under a private marker so the decorator + keyed factories share
		// one instance without re-resolving a captured descriptor.
		services.AddSingleton(sp => new RawCacheServiceMarker(implementationFactory(sp)));

		// Non-keyed: decorated with the "other" consumer tag.
		services.AddSingleton<ICacheService>(static sp =>
			new InstrumentedCacheService(sp.GetRequiredService<RawCacheServiceMarker>().Inner, "other"));

		// Keyed: decorated with specific consumer tags so cache metrics can be sliced by subsystem.
		services.AddKeyedSingleton<ICacheService>(CacheConsumers.QueryCaching, static (sp, _) =>
			new InstrumentedCacheService(sp.GetRequiredService<RawCacheServiceMarker>().Inner, CacheConsumers.QueryCaching));
		services.AddKeyedSingleton<ICacheService>(CacheConsumers.GrantResolution, static (sp, _) =>
			new InstrumentedCacheService(sp.GetRequiredService<RawCacheServiceMarker>().Inner, CacheConsumers.GrantResolution));

		return services;
	}

	private static void RemoveCacheServiceRegistrations(IServiceCollection services) {
		for (var i = services.Count - 1; i >= 0; i--) {
			var serviceType = services[i].ServiceType;
			if (serviceType == typeof(ICacheService) || serviceType == typeof(RawCacheServiceMarker)) {
				services.RemoveAt(i);
			}
		}
	}

	/// <summary>
	/// Internal marker that holds the raw (non-decorated) cache implementation so the decorator and keyed
	/// factories share a single instance.
	/// </summary>
	private sealed class RawCacheServiceMarker(ICacheService inner) {
		public ICacheService Inner => inner;
	}
}
