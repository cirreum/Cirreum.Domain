namespace Cirreum;

using Cirreum.Caching;
using Cirreum.Conductor;
using Cirreum.Conductor.Configuration;
using Cirreum.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Reflection;

/// <summary>
/// Extension methods for registering Cirreum.Conductor services.
/// </summary>
public static class ConductorServiceCollectionExtensions {

	private const string ConductorRegisteredKey = "__ConductorRegistered";

	/// <summary>
	/// Adds Conductor services using configuration-based settings and optional overrides.
	/// </summary>
	/// <param name="services">
	/// The <see cref="IServiceCollection"/> to add services to.
	/// </param>
	/// <param name="configuration">
	/// The application <see cref="IConfiguration"/> used to bind <see cref="ConductorSettings"/>.
	/// The configuration section defaults to <see cref="ConductorSettings.SectionName"/>,
	/// but can be overridden via <see cref="ConductorOptionsBuilder.WithConfigurationSection(string)"/>.
	/// </param>
	/// <param name="configureConductor">
	/// Optional action to configure the <see cref="ConductorBuilder"/>, typically used to
	/// register request handlers, notification handlers, and intercepts from one or more assemblies.
	/// If omitted, a no-op builder is used.
	/// </param>
	/// <param name="configureOptions">
	/// Optional action to customize <see cref="ConductorOptionsBuilder"/>, allowing overrides
	/// of configuration-bound settings (e.g., publisher strategy, cache behavior, dispatcher lifetime)
	/// and additional intercept configuration.
	/// </param>
	/// <returns>
	/// The <see cref="IServiceCollection"/> for chaining.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="services"/> or <paramref name="configuration"/> is <c>null</c>.
	/// </exception>
	public static IServiceCollection AddConductor(
		this IServiceCollection services,
		IConfiguration configuration,
		Action<ConductorBuilder>? configureConductor = null,
		Action<ConductorOptionsBuilder>? configureOptions = null) {

		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Options builder initialized with IConfiguration; will bind settings lazily.
		var optionsBuilder = new ConductorOptionsBuilder(configuration);

		// Allow caller (including higher-level APIs like AddDomainServices) to tweak options.
		configureOptions?.Invoke(optionsBuilder);

		// Ensure we always have a builder delegate.
		configureConductor ??= static _ => { };

		return services.AddConductorInternal(configureConductor, optionsBuilder, applyDefaultPipeline: false);
	}

	/// <summary>
	/// Adds Conductor services using code-based configuration only (no <see cref="IConfiguration"/>).
	/// </summary>
	/// <param name="services">
	/// The <see cref="IServiceCollection"/> to add services to.
	/// </param>
	/// <param name="configureConductor">
	/// Action to configure the <see cref="ConductorBuilder"/>, typically used to register
	/// request handlers, notification handlers, and intercepts from one or more assemblies.
	/// </param>
	/// <param name="configureOptions">
	/// Optional action to configure core Conductor options, such as dispatcher lifetime,
	/// publisher strategy, cache behavior, and the intercept pipeline.
	/// </param>
	/// <returns>
	/// The <see cref="IServiceCollection"/> for chaining.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="services"/> or <paramref name="configureConductor"/> is <c>null</c>.
	/// </exception>
	public static IServiceCollection AddConductor(
		this IServiceCollection services,
		Action<ConductorBuilder> configureConductor,
		Action<ConductorOptionsBuilder>? configureOptions = null) {

		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureConductor);

		var optionsBuilder = new ConductorOptionsBuilder();
		configureOptions?.Invoke(optionsBuilder);

		return services.AddConductorInternal(configureConductor, optionsBuilder, applyDefaultPipeline: false);
	}

	internal static IServiceCollection AddConductorInternal(
		this IServiceCollection services,
		Action<ConductorBuilder> configureConductor,
		ConductorOptionsBuilder optionsBuilder,
		bool applyDefaultPipeline) {

		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configureConductor);
		ArgumentNullException.ThrowIfNull(optionsBuilder);

		// Idempotency: prevent double-registration of Conductor on the same service collection.
		if (services.Any(sd => Equals(sd.ServiceKey, ConductorRegisteredKey))) {
			throw new InvalidOperationException(
				"Conductor services have already been registered. " +
				"AddConductor should only be called once per service collection. " +
				"If using higher-level convenience APIs (such as AddDomainServices), " +
				"do not call AddConductor directly.");
		}

		services.AddKeyedSingleton(ConductorRegisteredKey, new object());

		// Resolve settings and dispatcher lifetime from the options builder.
		var settings = optionsBuilder.GetSettings();
		var dispatcherLifetime = optionsBuilder.DispatcherLifetime;

		// Make settings visible to the rest of the graph
		services.AddSingleton(settings);

		// Register concrete Publisher
		services.TryAdd(ServiceDescriptor.Describe(
			typeof(Publisher),
			sp => new Publisher(
				sp,
				settings.PublisherStrategy,
				sp.GetRequiredService<ILogger<Publisher>>()),
			dispatcherLifetime));

		// Register concrete Dispatcher using configured lifetime.
		services.TryAdd(ServiceDescriptor.Describe(
			typeof(Dispatcher),
			sp => new Dispatcher(
				sp,
				sp.GetRequiredService<Publisher>()),
			dispatcherLifetime));

		// Register public facades with the same lifetime as Dispatcher.
		services.TryAdd(ServiceDescriptor.Describe(
			typeof(IPublisher),
			sp => sp.GetRequiredService<Publisher>(),
			dispatcherLifetime));

		services.TryAdd(ServiceDescriptor.Describe(
			typeof(IDispatcher),
			sp => sp.GetRequiredService<Dispatcher>(),
			dispatcherLifetime));

		services.TryAdd(ServiceDescriptor.Describe(
			typeof(IConductor),
			sp => sp.GetRequiredService<Dispatcher>(),
			dispatcherLifetime));

		// Safety nets: ensure cache infrastructure is available even if
		// AddCirreumCaching hasn't been called yet (e.g., standalone AddConductor usage).
		services.TryAddSingleton(new CacheSettings());
		services.TryAddSingleton<ICacheService, NoCacheService>();
		services.TryAddKeyedSingleton<ICacheService, NoCacheService>(CacheConsumers.QueryCaching);
		services.TryAddKeyedSingleton<ICacheService, NoCacheService>(CacheConsumers.GrantResolution);

		// Configure the Conductor builder (handlers, notifications, intercepts).
		var builder = new ConductorBuilder();

		// Let the caller register assemblies, handlers, and any custom intercepts first.
		configureConductor(builder);

		// Only apply the standard pipeline (Validation → Auth → [custom] → Perf → Cache)
		// when we're in the "domain" / opinionated path.
		if (applyDefaultPipeline) {
			optionsBuilder.ConfigureIntercepts(builder);
		}

		// Register operation handlers and notification handlers from configured assemblies.
		services.AddOperationHandlers([.. builder.Assemblies]);
		services.AddNotificationHandlers([.. builder.Assemblies]);

		// Register intercept descriptors in the order they were configured on the builder.
		foreach (var interceptDescriptor in builder.Intercepts) {
			services.Add(interceptDescriptor);
		}

		return services;

	}


	private static IServiceCollection AddOperationHandlers(
		this IServiceCollection services,
		Assembly[] assemblies) {

		var voidHandlerType = typeof(IOperationHandler<>);
		var typedHandlerType = typeof(IOperationHandler<,>);

		var availableTypes = assemblies
			.SelectMany(a => a.GetExportedTypes())
			.Where(t => t.IsConcreteClass())  // only concrete
			.Distinct();

		var handlers = from type in availableTypes
					   let voidInterface = type.GetFirstMatchingGenericInterface(voidHandlerType)
					   let typedInterface = type.GetFirstMatchingGenericInterface(typedHandlerType)
					   where voidInterface != null || typedInterface != null
					   select (type, voidInterface, typedInterface);

		foreach (var (handlerType, voidInterface, typedInterface) in handlers) {
			if (voidInterface != null) {
				services.TryAddTransient(voidInterface, handlerType);
			}
			if (typedInterface != null) {
				services.TryAddTransient(typedInterface, handlerType);
			}
		}

		return services;
	}

	private static IServiceCollection AddNotificationHandlers(
		this IServiceCollection services,
		Assembly[] assemblies) {

		var notificationHandlerType = typeof(INotificationHandler<>);

		var availableTypes = assemblies
			.SelectMany(a => a.GetExportedTypes())
			.Where(t => t.IsClass && !t.IsAbstract)
			.Distinct();

		// 1. Register closed generic handlers (concrete implementations)
		var closedHandlers = from type in availableTypes
							 where !type.IsGenericTypeDefinition
							 let matchingInterface = type.GetFirstMatchingGenericInterface(notificationHandlerType)
							 where matchingInterface != null
							 select (matchingInterface, type);

		foreach (var (handlerInterface, handlerType) in closedHandlers) {
			services.Add(ServiceDescriptor.Transient(handlerInterface, handlerType));
		}

		// 2. Register open generic handlers (like DistributedMessageHandler<>)
		var openHandlers = from type in availableTypes
						   where type.IsGenericTypeDefinition
						   let interfaces = type.GetInterfaces()
						   where interfaces.Any(i =>
							   i.IsGenericType &&
							   i.GetGenericTypeDefinition() == notificationHandlerType)
						   // Verify arity matches (INotificationHandler<> has 1 generic parameter)
						   where type.GetGenericArguments().Length == 1
						   select type;

		foreach (var handlerType in openHandlers) {
			// Register the open generic against the open generic interface
			services.Add(ServiceDescriptor.Transient(notificationHandlerType, handlerType));
		}

		return services;

	}

}