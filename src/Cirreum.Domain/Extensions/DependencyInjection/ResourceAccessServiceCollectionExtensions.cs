namespace Cirreum;

using Cirreum.Authorization.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Extension methods for registering the resource access evaluation system.
/// </summary>
public static class ResourceAccessServiceCollectionExtensions {

	/// <summary>
	/// Registers the resource access evaluation pipeline and configures
	/// <see cref="IAccessEntryProvider{T}"/> implementations via the builder delegate.
	/// </summary>
	/// <param name="services">The <see cref="IServiceCollection"/> to register with.</param>
	/// <param name="configure">
	/// A delegate that configures the <see cref="ResourceAccessBuilder"/> with provider
	/// registrations for each protected resource type.
	/// </param>
	/// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddResourceAccess(resources =&gt; {
	///     resources.AddProvider&lt;DocumentFolder, DocumentFolderAccessEntryProvider&gt;();
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddResourceAccess(
		this IServiceCollection services,
		Action<ResourceAccessBuilder> configure) {

		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		// Core evaluator — idempotent.
		services.TryAddScoped<IResourceAccessEvaluator, ResourceAccessEvaluator>();

		// Let the app register its providers.
		var builder = new ResourceAccessBuilder(services);
		configure(builder);

		return services;
	}

}
