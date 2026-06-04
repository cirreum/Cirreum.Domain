namespace Cirreum.Authorization;

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Data;
using System.Linq;

/// <summary>
/// Base implementation of the <see cref="IAuthorizationRoleRegistry"/>
/// </summary>
/// <param name="logger"></param>
public abstract class AuthorizationRoleRegistryBase(
	ILogger logger
) : IAuthorizationRoleRegistry {

	protected internal static readonly ConcurrentDictionary<string, Role> _registeredRoles = new();
	private static readonly ConcurrentDictionary<string, IImmutableSet<Role>> _effectiveRolesCache = new();


	protected ILogger _logger = logger;
	protected readonly Dictionary<Role, HashSet<Role>> _roleInheritance = [];
	protected bool _initialized;

	/// <summary>
	/// Standard initialization.
	/// </summary>
	/// <remarks>
	/// Registration is order dependent and this method ensures
	/// authorization is built correctly.
	/// </remarks>
	/// <returns>A <see cref="ValueTask"/>.</returns>
	protected ValueTask DefaultInitializationAsync() {

		if (this._initialized) {
			this._logger.LogWarning("InitializeAsync was called more than once. Ignoring subsequent calls.");
			return ValueTask.CompletedTask; // Prevent reinitialization
		}
		this._initialized = true;

		//
		// Register application defaults
		//
		this.RegisterDefaultRoleHierarchies();

		//
		// Register dynamic Roles and Permission
		//
		this.RegisterDynamicRoleDefinitions();

		//
		// Validate the complete hierarchy
		//
		this.ValidateCompleteHierarchy();

		//
		// Log the hierarchy
		//
		this.LogHierarchySummary();


		return ValueTask.CompletedTask;

	}

	/// <inheritdoc/>
	public IImmutableSet<Role> GetRegisteredRoles() {
		this.EnsureInitialized();
		return [.. _registeredRoles.Values];
	}

	/// <inheritdoc/>
	public IImmutableSet<Role> GetInheritingRoles(Role child) {
		this.EnsureInitialized();
		return [.. this._roleInheritance
			.Where(kv => kv.Value.Contains(child))
			.Select(kv => kv.Key)];
	}

	/// <inheritdoc/>
	public IImmutableSet<Role> GetInheritedRoles(Role parent) {
		this.EnsureInitialized();
		return this._roleInheritance.TryGetValue(parent, out var children)
			? [.. children]
			: [];
	}

	/// <inheritdoc/>
	public Role? GetRoleFromString(string roleString) {
		this.EnsureInitialized();
		return _registeredRoles.TryGetValue(roleString, out var role) ? role : null;
	}

	/// <inheritdoc/>
	public IImmutableSet<Role> GetEffectiveRoles(IEnumerable<Role> directRoles) {
		this.EnsureInitialized();

		// Create a cache key from the sorted direct roles
		var sortedRoles = directRoles.OrderBy(r => r.ToString()).ToArray();
		var cacheKey = string.Join("|", sortedRoles.Select(r => r.ToString()));

		// Return from cache if available
		if (_effectiveRolesCache.TryGetValue(cacheKey, out var cachedResult)) {
			return cachedResult;
		}

		// Calculate effective roles
		var effectiveRoles = new HashSet<Role>(sortedRoles);
		var queue = new Queue<Role>(sortedRoles);

		while (queue.Count > 0) {
			var role = queue.Dequeue();
			if (this._roleInheritance.TryGetValue(role, out var inheritedRoles)) {
				foreach (var inheritedRole in inheritedRoles) {
					if (effectiveRoles.Add(inheritedRole)) {
						queue.Enqueue(inheritedRole);
					}
				}
			}
		}

		var result = effectiveRoles.ToImmutableHashSet();

		// Cache the result
		_effectiveRolesCache.TryAdd(cacheKey, result);

		return result;
	}

	/// <summary>
	/// Scans all assemblies using the <see cref="RoleDefinitionScanner"/> and registers
	/// their defined roles and hierarchies.
	/// </summary>
	protected virtual void RegisterDynamicRoleDefinitions() {
		this._logger.LogDebug("Initializing dynamic role hierarchies...");

		var assemblyRoleDefinitions = RoleDefinitionScanner.ScanAssemblies(this._logger);

		// First, register all custom roles from all assemblies
		foreach (var (assembly, customRoles) in assemblyRoleDefinitions.CustomRoles) {
			foreach (var role in customRoles) {
				if (this.IsValidRole(role)) {
					_registeredRoles[role] = role;
					if (this._logger.IsEnabled(LogLevel.Debug)) {
						this._logger.LogDebug("Registered custom role: {Role} from {Assembly}", role, assembly);

					}
				}
			}
		}

		// Then, process hierarchies
		foreach (var (assembly, roleMap) in assemblyRoleDefinitions.Hierarchies) {
			// Filter out invalid role inheritance references and ensure distinct
			var validRoleInheritance = roleMap
				.Where(kv => this.IsValidRole(kv.Key))
				.ToDictionary(
					kv => kv.Key,
					kv => kv.Value
						.Where(this.IsValidRole)
						.Distinct()
						.ToArray());

			// Register role inheritance
			foreach (var (role, inheritedRoles) in validRoleInheritance) {
				if (inheritedRoles.Length > 0) {
					this.RegisterRoleInheritance(role, inheritedRoles);
				}
			}
		}
	}

	private void RegisterDefaultRoleHierarchies() {

		this._logger.LogDebug("Initializing default application role hierarchies...");

		// Register all application roles first
		foreach (var role in ApplicationRoles.GetRoles()) {
			_registeredRoles[role] = role;
		}

		this.RegisterRoleInheritance(ApplicationRoles.AppSystemRole,
			ApplicationRoles.AppAdminRole
		);

		this.RegisterRoleInheritance(ApplicationRoles.AppAdminRole,
			ApplicationRoles.AppManagerRole,
			ApplicationRoles.AppAgentRole
		);

		this.RegisterRoleInheritance(ApplicationRoles.AppManagerRole,
			ApplicationRoles.AppInternalRole
		);

		this.RegisterRoleInheritance(ApplicationRoles.AppAgentRole,
			ApplicationRoles.AppInternalRole
		);

		this.RegisterRoleInheritance(ApplicationRoles.AppInternalRole,
			ApplicationRoles.AppUserRole
		);

	}

	private void RegisterRoleInheritance(Role role, params Role[] inheritFrom) {
		ArgumentOutOfRangeException.ThrowIfZero(inheritFrom.Length);

		if (!this._roleInheritance.TryGetValue(role, out var value)) {
			value = [];
			this._roleInheritance[role] = value;
		}

		foreach (var inheritedRole in inheritFrom) {
			// Check for cycles before adding the inheritance
			if (this.WouldCreateCycle(role, inheritedRole)) {
				throw new InvalidOperationException(
					$"Invalid role inheritance: Adding {inheritedRole} as inherited role of {role} " +
					$"would create a circular reference in the role hierarchy.");
			}

			value.Add(inheritedRole);
			if (this._logger.IsEnabled(LogLevel.Debug)) {
				this._logger.LogDebug(
					"Registered inheritance: {ParentRole} -> {ChildRole}",
					role, inheritedRole);
			}
		}
	}

	private bool WouldCreateCycle(Role startRole, Role targetRole) {
		// Use breadth-first search to detect cycles
		var visited = new HashSet<Role>();
		var queue = new Queue<Role>();

		// Start with the targetRole (the role we're considering adding as an inherited role)
		queue.Enqueue(targetRole);

		while (queue.Count > 0) {
			var currentRole = queue.Dequeue();

			// If we've already visited this role, skip it
			if (!visited.Add(currentRole)) {
				continue;
			}

			// If the current role is the same as the start role, we've found a cycle
			if (currentRole == startRole) {
				return true;
			}

			// Get all roles that the current role inherits from
			if (this._roleInheritance.TryGetValue(currentRole, out var inheritedRoles)) {
				// Queue up all the inherited roles for processing
				foreach (var inheritedRole in inheritedRoles) {
					queue.Enqueue(inheritedRole);
				}
			}
		}

		// No cycle found
		return false;
	}

	private bool IsValidRole(Role role) {
		// Ensure if its an "app:xxx" role, it exists (no custom app:{custom} roles)
		if (role.IsApplicationRole && !ApplicationRoles.GetRoles().Contains(role)) {
			if (this._logger.IsEnabled(LogLevel.Warning)) {
				this._logger.LogWarning(
					"Invalid role '{Role}': Not a known application role",
					role);
				return false;
			}
		}

		return true;
	}

	private void ValidateCompleteHierarchy() {
		// Ensure all roles in inheritance relationships are registered
		var allRolesInHierarchy = this._roleInheritance.Keys
			.Concat(this._roleInheritance.Values.SelectMany(x => x))
			.Distinct();

		var unregisteredRoles = allRolesInHierarchy
			.Where(r => !_registeredRoles.ContainsKey(r.ToString()))
			.ToList();

		if (unregisteredRoles.Count != 0) {
			throw new InvalidOperationException(
				$"Found unregistered roles in hierarchy: {string.Join(", ", unregisteredRoles)}");
		}
	}

	private void LogHierarchySummary() {
		var totalRoles = _registeredRoles.Count;
		var totalRelationships = this._roleInheritance.Sum(x => x.Value.Count);
		if (this._logger.IsEnabled(LogLevel.Debug)) {
			this._logger.LogDebug(
				"Role hierarchy initialized with {TotalRoles} roles and {TotalRelationships} inheritance relationships",
				totalRoles,
				totalRelationships);
		}
	}

	private void EnsureInitialized() {
		if (!this._initialized) {
			throw new InvalidOperationException(
				"Role registry must be initialized before use. Call InitializeAsync first.");
		}
	}

}