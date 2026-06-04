namespace Cirreum;

using Cirreum.Authorization;
using Cirreum.Authorization.Operations;
using Cirreum.Conductor.Configuration;
using Cirreum.Extensions;
using Cirreum.Security;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Linq;
using System.Reflection;

/// <summary>
/// Extension methods that compose the domain service layer (Conductor + FluentValidation +
/// authorization) for a Cirreum application.
/// </summary>
public static class DomainServicesServiceCollectionExtensions {

	private const string DomainServicesRegisteredKey = "__DomainServicesRegistered";

	/// <summary>
	/// Tries to add the default built-in implementation of the
	/// <see cref="IAuthorizationEvaluator"/> service if one is not already registered.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
	public static void AddDefaultAuthorizationEvaluator(
		this IServiceCollection services) {
		services.TryAddScoped<IAuthorizationEvaluator, DefaultAuthorizationEvaluator>();
		services.TryAddScoped<IAuthorizationContextAccessor, DefaultAuthorizationContextAccessor>();
	}

	/// <summary>
	/// Registers the default domain context initializer.
	/// </summary>
	/// <param name="services">The current <see cref="IServiceCollection"/> to register with.</param>
	public static void AddDomainContextInitilizer(
		this IServiceCollection services) {
		services.TryAddSingleton<IDomainContextInitializer, DomainContextInitializer>();
	}

	/// <summary>
	/// Adds domain services using Conductor as the core dispatcher/publisher engine,
	/// binding settings from configuration and applying domain conventions.
	/// </summary>
	/// <param name="services">
	/// The <see cref="IServiceCollection"/> to add services to.
	/// </param>
	/// <param name="configuration">
	/// The application <see cref="IConfiguration"/> used to bind <see cref="ConductorSettings"/>
	/// and any additional domain-specific configuration.
	/// </param>
	/// <param name="configureConductorOptions">
	/// Optional callback that allows callers (or a higher-level <c>DomainBuilder</c>) to
	/// customize Conductor behavior via <see cref="ConductorOptionsBuilder"/>, including
	/// overrides to configuration-bound settings and dispatcher lifetime.
	/// </param>
	/// <returns>
	/// The <see cref="IServiceCollection"/> for chaining.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This overload is the "one-stop" registration method for most applications. It configures a
	/// standard Conductor pipeline with validation, authorization, performance monitoring, and
	/// query caching, and registers domain request/notification handlers from the scanned assemblies.
	/// </para>
	/// <para>
	/// Advanced scenarios can call
	/// <see cref="ConductorServiceCollectionExtensions.AddConductor(IServiceCollection, IConfiguration, Action{Cirreum.Conductor.ConductorBuilder}?, Action{ConductorOptionsBuilder}?)"/>
	/// directly for full control.
	/// </para>
	/// <para>
	/// The authentication boundary resolver is intentionally NOT registered here: it lives in the
	/// spine-reachable <c>Cirreum.AuthenticationProvider</c> and is wired by the server spine
	/// (<c>AddDefaultAuthenticationBoundaryResolver</c>), so it is available even to apps that do
	/// not call <c>AddDomainServices</c>.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentNullException">
	/// Thrown if <paramref name="services"/> or <paramref name="configuration"/> is <c>null</c>.
	/// </exception>
	public static IServiceCollection AddDomainServices(
		this IServiceCollection services,
		IConfiguration configuration,
		Action<ConductorOptionsBuilder>? configureConductorOptions = null) {

		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		// Idempotency check
		if (services.Any(sd => sd.ServiceKey?.ToString() == DomainServicesRegisteredKey)) {
			throw new InvalidOperationException(
				"Domain services have already been registered. " +
				"Call AddDomainServices only once per service collection.");
		}
		services.AddKeyedSingleton(DomainServicesRegisteredKey, new object());

		// Use the assembly scanner to find all relevant assemblies.
		var assemblies = AssemblyScanner.ScanAssemblies().ToArray();

		// Central cache infrastructure.
		services.AddCirreumCaching(configuration);

		// FluentValidation + authorization (validators, constraints, resource + policy authorizers).
		services.AddFluentValidationAndAuthorization(assemblies);

		// Conductor — call the internal core with applyDefaultPipeline: true (the built-in
		// domain pipeline: validation, authorization, performance, caching).
		var optionsBuilder = new ConductorOptionsBuilder(configuration) {
			CustomInterceptsAllowed = true
		};
		configureConductorOptions?.Invoke(optionsBuilder);
		services.AddConductorInternal(
			configureConductor: conductor => conductor.RegisterFromAssemblies(assemblies),
			optionsBuilder: optionsBuilder,
			applyDefaultPipeline: true);

		// Domain context initializer.
		services.AddDomainContextInitilizer();

		return services;
	}

	private static IServiceCollection AddFluentValidationAndAuthorization(this IServiceCollection services, params Assembly?[] assemblies) {

		var validatorOpenGenericType = typeof(IValidator<>);
		var constraintType = typeof(IAuthorizationConstraint);
		var resourceAuthorizerType = typeof(IAuthorizer<>);
		var policyAuthorizerType = typeof(IPolicyValidator);

		var availableTypes = assemblies
			.Where(a => a is not null)
			.SelectMany(a => a!.GetTypes())
			.Distinct();

		// Normal domain validators — excludes resource authorizers (they implement IValidator<T>
		// too, but are registered separately below).
		var normalValidators = from type in availableTypes
							   where type.IsConcreteClass() &&
									 !type.ImplementsGenericInterface(resourceAuthorizerType)
							   let matchingInterface = type.GetFirstMatchingGenericInterface(validatorOpenGenericType)
							   where matchingInterface != null
							   select (matchingInterface, type);

		foreach (var (validatorInterface, validatorType) in normalValidators) {
			services.TryAddEnumerable(new ServiceDescriptor(
				serviceType: validatorInterface,
				implementationType: validatorType,
				lifetime: ServiceLifetime.Transient));
			services.TryAddTransient(validatorType, validatorType);
		}

		// Authorization constraints (Stage 1 Step 1).
		var authConstraints = from type in availableTypes
							  where type.IsConcreteClass() &&
									type.IsAssignableTo(constraintType)
							  select type;

		foreach (var constraint in authConstraints) {
			services.TryAddEnumerable(new ServiceDescriptor(
				serviceType: constraintType,
				implementationType: constraint,
				lifetime: ServiceLifetime.Transient));
			services.AddTransient(constraint, constraint);
		}

		// Resource authorizers.
		var resourceAuthorizers = from type in availableTypes
								  where type.IsConcreteClass()
								  let matchingInterface = type.GetFirstMatchingGenericInterface(resourceAuthorizerType)
								  where matchingInterface != null
								  select (matchingInterface, type);

		foreach (var (authorizerInterface, authorizerType) in resourceAuthorizers) {
			services.TryAddEnumerable(new ServiceDescriptor(
				serviceType: authorizerInterface,
				implementationType: authorizerType,
				lifetime: ServiceLifetime.Transient));
			services.AddTransient(authorizerType, authorizerType);
		}

		// Policy authorizers.
		var policyAuthValidators = from type in availableTypes
								   where type.IsConcreteClass() &&
										 type.IsAssignableTo(policyAuthorizerType)
								   select type;

		foreach (var validator in policyAuthValidators) {
			services.TryAddEnumerable(new ServiceDescriptor(
				serviceType: policyAuthorizerType,
				implementationType: validator,
				lifetime: ServiceLifetime.Transient));
			services.AddTransient(validator, validator);
		}

		return services;
	}

}
