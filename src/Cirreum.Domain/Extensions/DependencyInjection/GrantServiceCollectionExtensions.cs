namespace Cirreum;

using Cirreum.Authorization.Operations.Grants;
using Cirreum.Authorization.Operations.Grants.Caching;
using Cirreum.Caching;
using Cirreum.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

/// <summary>
/// Extension methods for registering Cirreum grant-based access control services.
/// </summary>
public static class GrantServiceCollectionExtensions {

	/// <summary>
	/// Registers the grant-based access control pipeline with a single universal grant resolver.
	/// Registers the app-provided <see cref="IOperationGrantProvider"/>, the framework-supplied
	/// <see cref="OperationGrantFactory"/> orchestrator, and the sealed
	/// <see cref="OperationGrantEvaluator"/> that enforces Mutate/Lookup/Search/Self grant semantics
	/// as Stage 1 Step 0 of the authorization pipeline.
	/// </summary>
	/// <typeparam name="TGrantResolver">The app's grant-resolver implementation.</typeparam>
	/// <param name="services">The <see cref="IServiceCollection"/> to register with.</param>
	/// <param name="lifetime">
	/// The lifetime of both the grant resolver and the orchestrator. Default is
	/// <see cref="ServiceLifetime.Scoped"/> because grant lookups usually need a scoped
	/// repository/DB context.
	/// </param>
	/// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
	public static IServiceCollection AddOperationGrants<TGrantResolver>(
		this IServiceCollection services,
		ServiceLifetime lifetime = ServiceLifetime.Scoped)
		where TGrantResolver : class, IOperationGrantProvider =>
		services.AddOperationGrantsCore(typeof(TGrantResolver), lifetime);

	/// <summary>
	/// Discovers the <see cref="IOperationGrantProvider"/> implementation in the provided assemblies,
	/// binds <see cref="OperationGrantCacheSettings"/> from configuration, and registers the full
	/// grants pipeline. The convention-based entry point intended for higher-level runtime extensions.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to register with.</param>
	/// <param name="configuration">
	/// The application <see cref="IConfiguration"/> used to bind <see cref="OperationGrantCacheSettings"/>
	/// from the <c>Cirreum:Authorization:Grants:Cache</c> section.
	/// </param>
	/// <param name="assemblies">The assemblies to scan for grant resolver implementations.</param>
	/// <param name="configureCaching">
	/// Optional delegate to override cache settings beyond what configuration provides.
	/// Applied after binding from <paramref name="configuration"/>.
	/// </param>
	/// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
	public static IServiceCollection AddGrantAuthorization(
		this IServiceCollection services,
		IConfiguration? configuration = null,
		Assembly[]? assemblies = null,
		Action<OperationGrantCacheSettings>? configureCaching = null) {

		ArgumentNullException.ThrowIfNull(services);

		assemblies ??= [.. AssemblyScanner.ScanAssemblies()];

		// Register cache settings from configuration + delegate (idempotent).
		services.AddGrantCacheInfrastructure(configuration, configureCaching);

		var grantResolverType = typeof(IOperationGrantProvider);

		var resolverType = assemblies
			.Where(a => a is not null)
			.SelectMany(a => a!.GetTypes())
			.Distinct()
			.FirstOrDefault(t => t.IsConcreteClass() && grantResolverType.IsAssignableFrom(t));

		if (resolverType is not null) {
			services.AddOperationGrantsCore(resolverType, ServiceLifetime.Scoped);
		}

		return services;
	}

	private static IServiceCollection AddOperationGrantsCore(
		this IServiceCollection services,
		Type resolverType,
		ServiceLifetime lifetime) {

		ArgumentNullException.ThrowIfNull(services);

		// Skip if already registered.
		if (services.Any(sd => sd.ServiceType == typeof(IOperationGrantProvider))) {
			return services;
		}

		// Resolver and orchestrator.
		services.Add(ServiceDescriptor.Describe(
			serviceType: typeof(IOperationGrantProvider),
			implementationType: resolverType,
			lifetime: lifetime));

		services.Add(ServiceDescriptor.Describe(
			serviceType: typeof(IOperationGrantFactory),
			implementationType: typeof(OperationGrantFactory),
			lifetime: lifetime));

		// Shared infrastructure.
		services.TryAddScoped<IOperationGrantAccessor, DefaultOperationGrantAccessor>();
		services.TryAddScoped<OperationGrantEvaluator>();
		services.AddGrantCacheInfrastructure();

		return services;
	}

	/// <summary>
	/// Registers the shared cache infrastructure for the grant system. Idempotent — safe to
	/// call from every grants registration invocation.
	/// </summary>
	private static void AddGrantCacheInfrastructure(
		this IServiceCollection services,
		IConfiguration? configuration = null,
		Action<OperationGrantCacheSettings>? configureCaching = null) {

		// Only register once.
		if (services.Any(sd => sd.ServiceType == typeof(OperationGrantCacheSettings))) {
			return;
		}

		var settings = new OperationGrantCacheSettings();

		if (configuration is not null) {
			var section = configuration.GetSection(OperationGrantCacheSettings.SectionPath);
			section.Bind(settings);
		}

		configureCaching?.Invoke(settings);
		services.AddSingleton(settings);

		services.TryAddSingleton<IOperationGrantCacheInvalidator, OperationGrantCacheInvalidator>();

		// Safety net: ensure a cache service is available even if Conductor caching hasn't been
		// configured yet. NoCacheService degrades gracefully — grants resolve on every request
		// without L2 caching.
		services.TryAddSingleton<ICacheService, NoCacheService>();
		services.TryAddKeyedSingleton<ICacheService, NoCacheService>(CacheConsumers.GrantResolution);
	}

}
