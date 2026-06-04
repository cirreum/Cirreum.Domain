namespace Cirreum;

using Cirreum.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Linq;

/// <summary>
/// Extension methods for registering Cirreum's centralized cache infrastructure.
/// </summary>
public static class CacheServiceCollectionExtensions {

	/// <summary>
	/// Registers the centralized <see cref="CacheSettings"/> and the
	/// <see cref="ICacheService"/> implementation selected by
	/// <see cref="CacheSettings.Provider"/>. Idempotent — safe to call
	/// from multiple subsystem registrations.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to register with.</param>
	/// <param name="configuration">
	/// Optional <see cref="IConfiguration"/> used to bind settings from the
	/// <c>Cirreum:Cache</c> section.
	/// </param>
	/// <param name="configureCaching">
	/// Optional delegate to override cache settings beyond what configuration provides.
	/// Applied after binding from <paramref name="configuration"/>.
	/// </param>
	/// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
	public static IServiceCollection AddCirreumCaching(
		this IServiceCollection services,
		IConfiguration? configuration = null,
		Action<CacheSettings>? configureCaching = null) {

		ArgumentNullException.ThrowIfNull(services);

		// Idempotent — only register once
		if (services.Any(sd => sd.ServiceType == typeof(CacheSettings))) {
			return services;
		}

		var settings = new CacheSettings();

		if (configuration is not null) {
			var section = configuration.GetSection(CacheSettings.SectionPath);
			section.Bind(settings);
		}

		configureCaching?.Invoke(settings);
		services.AddSingleton(settings);

		// Scoped cache key context for upstream pipeline stages (e.g., grant evaluation)
		// to stamp key prefixes and extra tags consumed by QueryCaching.
		services.TryAddScoped<CacheKeyContext>();

		// Register the cache service based on the provider
		AddCacheableQueryService(services, settings);

		// Wrap the concrete ICacheService with the telemetry decorator and
		// register keyed instances for known subsystems (query-caching,
		// grant-resolution). Skips decoration for NoCacheService.
		DecorateWithInstrumentation(services);

		return services;
	}

	private static void AddCacheableQueryService(
		IServiceCollection services,
		CacheSettings settings) {

		// Use Replace for None/InMemory to enforce config over code registration.
		// Use TryAdd for Distributed/Hybrid to allow infrastructure packages to
		// provide their own implementation.
		switch (settings.Provider) {
			case CacheProvider.None:
				services.Replace(ServiceDescriptor.Singleton<ICacheService, NoCacheService>());
				break;

			case CacheProvider.InMemory:
				services.Replace(ServiceDescriptor.Singleton<ICacheService, InMemoryCacheService>());
				break;

			case CacheProvider.Distributed:
			case CacheProvider.Hybrid:
				// Infrastructure packages (Cirreum.QueryCache.Distributed, Cirreum.QueryCache.Hybrid)
				// register the real implementation. Fall back to NoCacheService if not registered.
				services.TryAddSingleton<ICacheService, NoCacheService>();
				break;
		}
	}

	/// <summary>
	/// Wraps the concrete <see cref="ICacheService"/> with <see cref="InstrumentedCacheService"/>
	/// and registers keyed instances for known subsystems. Skips wrapping entirely when the
	/// concrete implementation is <see cref="NoCacheService"/> — there's no cache to observe.
	/// </summary>
	private static void DecorateWithInstrumentation(IServiceCollection services) {
		var descriptor = services.FirstOrDefault(d =>
			d.ServiceType == typeof(ICacheService) && !d.IsKeyedService);
		if (descriptor is null) {
			return;
		}

		// NoCacheService is a pass-through — no telemetry needed.
		// Register it directly for keyed consumers and skip the decorator.
		if (descriptor.ImplementationType == typeof(NoCacheService)) {
			services.TryAddKeyedSingleton<ICacheService>(CacheConsumers.QueryCaching, (_, _) => new NoCacheService());
			services.TryAddKeyedSingleton<ICacheService>(CacheConsumers.GrantResolution, (_, _) => new NoCacheService());
			return;
		}

		// For real cache implementations, replace the non-keyed registration with
		// a decorator that adds telemetry, then register keyed instances that share
		// the same inner implementation with per-consumer tags.
		services.Remove(descriptor);

		// Register the raw inner implementation under a private marker so the
		// decorator and keyed factories can resolve it without re-parsing the
		// original ServiceDescriptor (which is fragile across .NET versions).
		services.Add(ServiceDescriptor.Describe(
			typeof(RawCacheServiceMarker),
			sp => new RawCacheServiceMarker(
				(ICacheService)ActivatorUtilities.CreateInstance(
					sp, descriptor.ImplementationType!)),
			descriptor.Lifetime));

		// Non-keyed: decorated with "other" consumer tag
		services.Add(ServiceDescriptor.Describe(
			typeof(ICacheService),
			sp => new InstrumentedCacheService(
				sp.GetRequiredService<RawCacheServiceMarker>().Inner, "other"),
			descriptor.Lifetime));

		// Keyed: decorated with specific consumer tags
		services.TryAddKeyedSingleton<ICacheService>(
			CacheConsumers.QueryCaching,
			(sp, _) => new InstrumentedCacheService(
				sp.GetRequiredService<RawCacheServiceMarker>().Inner,
				CacheConsumers.QueryCaching));

		services.TryAddKeyedSingleton<ICacheService>(
			CacheConsumers.GrantResolution,
			(sp, _) => new InstrumentedCacheService(
				sp.GetRequiredService<RawCacheServiceMarker>().Inner,
				CacheConsumers.GrantResolution));
	}

	/// <summary>
	/// Internal marker that holds the raw (non-decorated) cache implementation.
	/// Prevents the decorator from needing to re-resolve from a captured
	/// <see cref="ServiceDescriptor"/>, which is fragile across .NET versions.
	/// </summary>
	private sealed class RawCacheServiceMarker(ICacheService inner) {
		public ICacheService Inner => inner;
	}
}
