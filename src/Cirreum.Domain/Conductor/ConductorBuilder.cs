namespace Cirreum.Conductor;

using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

/// <summary>
/// Provides a builder for configuring and registering assemblies, services, and intercepts for conductor-based
/// dependency injection pipelines.
/// </summary>
/// <remarks>Use <see cref="ConductorBuilder"/> to fluently register assemblies and intercept implementations
/// required for conductor discovery and configuration. This builder supports automatic service registration from
/// assemblies, flexible intercept registration (including open and closed generic types), and chaining of configuration
/// methods for streamlined setup. Thread safety is not guaranteed; concurrent modifications should be externally
/// synchronized if required.</remarks>
public sealed class ConductorBuilder {

	internal List<Assembly> Assemblies { get; } = [];

	/// <summary>
	/// List of intercepts to register.
	/// </summary>
	public List<ServiceDescriptor> Intercepts { get; } = [];

	/// <summary>
	/// Registers all eligible services from the assembly that contains the specified type parameter.
	/// </summary>
	/// <remarks>Only services defined in the same assembly as <typeparamref name="T"/> will be registered. This
	/// method is useful for automatically discovering and registering services without specifying the assembly
	/// explicitly.</remarks>
	/// <typeparam name="T">The type whose containing assembly will be scanned for service registrations.</typeparam>
	/// <returns>The current <see cref="ConductorBuilder"/> instance to allow for method chaining.</returns>
	public ConductorBuilder RegisterFromAssemblyContaining<T>()
		=> this.RegisterFromAssemblyContaining(typeof(T));

	/// <summary>
	/// Registers all services from the assembly that contains the specified type for dependency injection.
	/// </summary>
	/// <remarks>Use this method to conveniently register services from an assembly without specifying the assembly
	/// directly. This is useful when the assembly is not known at compile time or when organizing registrations by type
	/// location.</remarks>
	/// <param name="type">A type used to identify the target assembly. All services defined in the assembly containing this type will be
	/// registered.</param>
	/// <returns>The current instance of <see cref="ConductorBuilder"/> to allow for method chaining.</returns>
	public ConductorBuilder RegisterFromAssemblyContaining(Type type)
		=> this.RegisterFromAssembly(type.Assembly);

	/// <summary>
	/// Registers the specified assembly for conductor discovery and configuration.
	/// </summary>
	/// <param name="assembly">The assembly to be added for conductor registration. Cannot be null.</param>
	/// <returns>The current instance of <see cref="ConductorBuilder"/> to allow method chaining.</returns>
	public ConductorBuilder RegisterFromAssembly(Assembly assembly) {
		this.Assemblies.Add(assembly);

		return this;
	}

	/// <summary>
	/// Registers types from the specified assemblies for use with the conductor builder.
	/// </summary>
	/// <param name="assemblies">An array of assemblies from which types will be registered. Cannot be null.</param>
	/// <returns>The current instance of <see cref="ConductorBuilder"/> to allow for method chaining.</returns>
	public ConductorBuilder RegisterFromAssemblies(params Assembly[] assemblies) {
		this.Assemblies.AddRange(assemblies);

		return this;
	}

	/// <summary>
	/// Adds an intercept registration for the specified service and implementation types with the given service lifetime.
	/// </summary>
	/// <typeparam name="TServiceType">The type of the service to intercept. This is typically an interface or base class that consumers will request.</typeparam>
	/// <typeparam name="TImplementationType">The concrete type that implements the service and will be intercepted. Must be assignable to <typeparamref
	/// name="TServiceType"/>.</typeparam>
	/// <param name="serviceLifetime">The lifetime with which the service will be registered. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
	/// <returns>The current <see cref="ConductorBuilder"/> instance, enabling fluent configuration.</returns>
	public ConductorBuilder AddIntercept<TServiceType, TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient)
		=> this.AddIntercept(typeof(TServiceType), typeof(TImplementationType), serviceLifetime);

	/// <summary>
	/// Adds an intercept of the specified implementation type to the conductor pipeline with the given service lifetime.
	/// </summary>
	/// <typeparam name="TImplementationType">The type of the intercept implementation to add to the pipeline.</typeparam>
	/// <param name="serviceLifetime">The lifetime with which the intercept implementation will be registered. Defaults to <see
	/// cref="ServiceLifetime.Transient"/>.</param>
	/// <returns>The current <see cref="ConductorBuilder"/> instance, enabling fluent configuration.</returns>
	public ConductorBuilder AddIntercept<TImplementationType>(ServiceLifetime serviceLifetime = ServiceLifetime.Transient) {
		return this.AddIntercept(typeof(TImplementationType), serviceLifetime);
	}

	/// <summary>
	/// Registers an intercept implementation type with the specified service lifetime for use in the conductor pipeline.
	/// </summary>
	/// <remarks>Each intercept implementation type must implement at least one closed generic interface of <see
	/// cref="IIntercept{TOperation, TResultValue}"/>. Multiple intercepts can be registered by calling this method multiple
	/// times.</remarks>
	/// <param name="implementationType">The type that implements one or more closed generic versions of <see cref="IIntercept{TOperation, TResultValue}"/>. This
	/// type will be registered as an intercept in the pipeline.</param>
	/// <param name="serviceLifetime">The lifetime with which the intercept implementation is registered. Defaults to <see
	/// cref="ServiceLifetime.Transient"/> if not specified.</param>
	/// <returns>The current <see cref="ConductorBuilder"/> instance, enabling fluent configuration.</returns>
	/// <exception cref="InvalidOperationException">Thrown if <paramref name="implementationType"/> does not implement any closed generic version of <see
	/// cref="IIntercept{TOperation, TResultValue}"/>.</exception>
	public ConductorBuilder AddIntercept(Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient) {

		var implementedGenericInterfaces = implementationType
			.GetInterfaces()
			.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IIntercept<,>))
			.ToList();

		if (implementedGenericInterfaces.Count == 0) {
			throw new InvalidOperationException(
				$"{implementationType.Name} must implement {typeof(IIntercept<,>).FullName}");
		}

		foreach (var implementedInterceptType in implementedGenericInterfaces) {
			this.Intercepts.Add(new ServiceDescriptor(
				implementedInterceptType,
				implementationType,
				serviceLifetime));
		}

		return this;
	}

	/// <summary>
	/// Adds an intercept service descriptor to the builder, specifying the service type, implementation type, and service
	/// lifetime.
	/// </summary>
	/// <remarks>Use this method to register intercepts for services that require custom behavior or middleware.
	/// Multiple intercepts can be added by calling this method repeatedly.</remarks>
	/// <param name="serviceType">The type of the service to intercept. This must be a valid service contract type.</param>
	/// <param name="implementationType">The type that implements the intercepted service. Must be assignable to <paramref name="serviceType"/>.</param>
	/// <param name="serviceLifetime">The lifetime with which the intercept service will be registered. Defaults to <see
	/// cref="ServiceLifetime.Transient"/>.</param>
	/// <returns>The current <see cref="ConductorBuilder"/> instance, enabling fluent configuration.</returns>
	public ConductorBuilder AddIntercept(Type serviceType, Type implementationType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient) {
		this.Intercepts.Add(new ServiceDescriptor(serviceType, implementationType, serviceLifetime));
		return this;
	}

	/// <summary>
	/// Registers an open generic intercept type for use with the conductor, specifying its service lifetime.
	/// </summary>
	/// <remarks>Use this method to add custom intercept logic to the conductor pipeline by registering open generic
	/// intercept types. Multiple intercepts can be registered by calling this method multiple times.</remarks>
	/// <param name="openInterceptType">The open generic type that implements the IIntercept&lt;,&gt; interface to be
	/// registered as an intercept. Must be a generic type that implements IIntercept&lt;,&gt;.</param>
	/// <param name="serviceLifetime">The lifetime with which the intercept type will be registered in the service container. Defaults to
	/// ServiceLifetime.Transient.</param>
	/// <returns>The current ConductorBuilder instance, enabling fluent configuration.</returns>
	/// <exception cref="InvalidOperationException">Thrown if openInterceptType is not a generic type or does not implement the IIntercept&lt;,&gt; interface.</exception>
	public ConductorBuilder AddOpenIntercept(Type openInterceptType, ServiceLifetime serviceLifetime = ServiceLifetime.Transient) {

		if (!openInterceptType.IsGenericType) {
			throw new InvalidOperationException($"{openInterceptType.Name} must be generic");
		}

		var implementedGenericInterfaces = openInterceptType
			.GetInterfaces()
			.Where(i => i.IsGenericType)
			.Select(i => i.GetGenericTypeDefinition());
		var implementedOpenInterceptInterfaces =
			new HashSet<Type>(implementedGenericInterfaces.Where(i => i == typeof(IIntercept<,>)));

		if (implementedOpenInterceptInterfaces.Count == 0) {
			throw new InvalidOperationException($"{openInterceptType.Name} must implement {typeof(IIntercept<,>).FullName}");
		}

		foreach (var openInterceptInterface in implementedOpenInterceptInterfaces) {
			this.Intercepts.Add(new ServiceDescriptor(openInterceptInterface, openInterceptType, serviceLifetime));
		}

		return this;
	}

	/// <summary>
	/// Adds multiple open generic intercept types to the conductor builder with the specified service lifetime.
	/// </summary>
	/// <param name="openInterceptTypes">A collection of open generic types representing intercepts to be registered. Each type must be an open generic
	/// type.</param>
	/// <param name="serviceLifetime">The service lifetime to use when registering the intercept types. The default is <see
	/// cref="ServiceLifetime.Transient"/>.</param>
	/// <returns>The current <see cref="ConductorBuilder"/> instance for chaining additional configuration calls.</returns>
	public ConductorBuilder AddOpenIntercepts(IEnumerable<Type> openInterceptTypes, ServiceLifetime serviceLifetime = ServiceLifetime.Transient) {
		foreach (var openInterceptType in openInterceptTypes) {
			this.AddOpenIntercept(openInterceptType, serviceLifetime);
		}

		return this;
	}

}