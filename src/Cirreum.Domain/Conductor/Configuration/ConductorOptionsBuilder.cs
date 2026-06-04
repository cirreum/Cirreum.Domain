namespace Cirreum.Conductor.Configuration;

using Cirreum.Conductor.Intercepts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder for configuring Conductor options during service registration.
/// </summary>
public class ConductorOptionsBuilder {

	internal ConductorOptionsBuilder() {

	}

	/// <summary>
	/// Initializes a new instance of the ConductorOptionsBuilder class using the specified configuration source.
	/// </summary>
	/// <param name="configuration">The configuration source that provides settings for the options builder. Cannot be null.</param>
	public ConductorOptionsBuilder(IConfiguration configuration) {
		this._configuration = configuration;
	}

	private readonly IConfiguration? _configuration;
	private string _configurationSection = ConductorSettings.SectionName;
	private ConductorSettings? _settings;
	private readonly List<Action<ConductorBuilder>> _interceptConfigurations = [];
	private ServiceLifetime _dispatcherLifetime = ServiceLifetime.Transient;

	/// <summary>
	/// Manually configures Conductor settings, bypassing appsettings.json.
	/// </summary>
	/// <param name="configure">Action to configure settings.</param>
	/// <returns>The builder for method chaining.</returns>
	public ConductorOptionsBuilder ConfigureSettings(Action<ConductorSettings> configure) {
		ArgumentNullException.ThrowIfNull(configure);
		this._settings ??= new ConductorSettings();
		configure(this._settings);
		return this;
	}

	/// <summary>
	/// Adds custom intercepts to the Conductor pipeline.
	/// Intercepts added here will be inserted AFTER authorization but BEFORE performance monitoring.
	/// </summary>
	/// <param name="configure">Action to configure intercepts.</param>
	/// <returns>The builder for method chaining.</returns>
	/// <remarks>
	/// This extensibility point allows you to add custom cross-cutting concerns that should run:
	/// <list type="bullet">
	/// <item>AFTER validation and authorization (security is enforced)</item>
	/// <item>BEFORE performance monitoring (your intercept execution is measured)</item>
	/// <item>BEFORE query caching (your intercept can affect what gets cached)</item>
	/// </list>
	/// Common use cases include: tenant isolation, audit logging, request transformation, 
	/// business-specific validation, or custom telemetry.
	/// </remarks>
	public ConductorOptionsBuilder AddCustomIntercepts(Action<ConductorBuilder> configure) {
		ArgumentNullException.ThrowIfNull(configure);

		if (!this.CustomInterceptsAllowed) {
			throw new InvalidOperationException(
				"Custom intercepts are only allowed when using AddDomainServices. " +
				"When using AddConductor directly, manually register intercepts using " +
				"builder.AddOpenIntercept(...) instead.");
		}

		this._interceptConfigurations.Add(configure);
		return this;
	}

	/// <summary>
	/// Configures the service lifetime for the Dispatcher/Conductor.
	/// Defaults to <see cref="ServiceLifetime.Transient"/>.
	/// </summary>
	public ConductorOptionsBuilder WithLifetime(ServiceLifetime lifetime) {
		this._dispatcherLifetime = lifetime;
		return this;
	}

	/// <summary>
	/// Overrides the default configuration section (<c>"Cirreum:Conductor"</c>)
	/// used to bind <see cref="ConductorSettings"/> during application setup.
	/// </summary>
	/// <param name="sectionName">
	/// The configuration section name to use.
	/// This value is required and replaces the default
	/// <see cref="ConductorSettings.SectionName"/> (<c>"Cirreum:Conductor"</c>).
	/// </param>
	/// <returns>
	/// The current <see cref="ConductorOptionsBuilder"/> instance
	/// to enable fluent chaining.
	/// </returns>
	/// <exception cref="ArgumentException">
	/// Thrown if <paramref name="sectionName"/> is null, empty, or whitespace.
	/// </exception>
	public ConductorOptionsBuilder WithConfigurationSection(string sectionName) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);
		this._configurationSection = sectionName;
		return this;
	}

	/// <summary>
	/// Sets the specified settings for the Conductor options builder.
	/// </summary>
	/// <param name="settings">The settings to apply to the builder. Cannot be null.</param>
	/// <returns>The current instance of <see cref="ConductorOptionsBuilder"/> with the specified settings applied.</returns>
	public ConductorOptionsBuilder WithSetting(ConductorSettings settings) {
		ArgumentNullException.ThrowIfNull(settings);
		this._settings = settings;
		return this;
	}

	internal bool CustomInterceptsAllowed { get; set; } = false;

	internal ServiceLifetime DispatcherLifetime => this._dispatcherLifetime;

	internal ConductorSettings GetSettings() {
		// Priority 1: Manual settings (explicit ConfigureSettings call)
		if (this._settings is not null) {
			return this._settings;
		}

		// Priority 2: Configuration binding (explicit BindConfiguration call)
		if (this._configuration is not null) {
			this._settings = new ConductorSettings();
			this._configuration.GetSection(this._configurationSection).Bind(this._settings);
			return this._settings;
		}

		// Priority 3: Defaults (no configuration provided)
		return new ConductorSettings();
	}

	internal void ConfigureIntercepts(ConductorBuilder builder) {

		// Core spine intercepts in fixed order.
		// Authorization-track intercepts (Authorization<,>, GrantedLookupAudit<,>) are
		// registered separately by the runtime composition layer — the spine
		// does not reference the authorization track (no cross-track references).
		builder
			.AddOpenIntercept(typeof(Validation<,>), this._dispatcherLifetime);

		// Custom intercepts (extensibility point — including track-specific intercepts
		// wired by provider runtimes such as Authorization).
		foreach (var config in this._interceptConfigurations) {
			config(builder);
		}

		// Wrapping and pre-emptive intercepts
		builder
			.AddOpenIntercept(typeof(HandlerPerformance<,>), this._dispatcherLifetime)
			.AddOpenIntercept(typeof(QueryCaching<,>), this._dispatcherLifetime);

	}

}