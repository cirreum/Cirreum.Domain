namespace Cirreum.Authorization;

using Cirreum.Extensions;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

/// <summary>
/// Scans assemblies for role definitions including hierarchies and privileges
/// </summary>
public static class RoleDefinitionScanner {

	/// <summary>
	/// Results from scanning role definitions
	/// </summary>
	public record RoleDefinitionScanResult(
		Dictionary<string, HashSet<Role>> CustomRoles,
		Dictionary<string, Dictionary<Role, Role[]>> Hierarchies
	);

	/// <summary>
	/// Scans assemblies for role definitions
	/// </summary>
	public static RoleDefinitionScanResult ScanAssemblies(ILogger logger) {
		ArgumentNullException.ThrowIfNull(logger);

		logger.LogScanningAssemblies();

		var hierarchies = new Dictionary<string, Dictionary<Role, Role[]>>();
		var allRoles = new Dictionary<string, HashSet<Role>>();
		var relationshipCount = 0;
		var roleCount = 0;

		foreach (var type in AssemblyScanner.ScanExportedTypes(IsRoleDefinitionProvider)) {

			var assemblyName = type.Assembly.GetName().Name;

			if (string.IsNullOrEmpty(assemblyName)) {
				logger.LogMissingAssemblyName(type.Assembly.FullName!, type.AssemblyQualifiedName);
				continue;
			}

			if (TryScanType(type, logger, out var roles, out var roleMap)) {

				// Handle previous implementations in same assembly
				var hadPreviousHierarchy = hierarchies.TryGetValue(assemblyName, out var previousHierarchy);
				var hadPreviousRoles = allRoles.TryGetValue(assemblyName, out var previousRoles);

				if (hadPreviousHierarchy && previousHierarchy != null) {
					relationshipCount -= previousHierarchy.Count;
				}

				if (hadPreviousRoles && previousRoles != null) {
					roleCount -= previousRoles.Count;
				}

				if (hadPreviousHierarchy || hadPreviousRoles) {
					logger.LogAdditionalImplementation(assemblyName, type.AssemblyQualifiedName);
				}

				// Add new counts
				relationshipCount += roleMap.Count;
				roleCount += roles.Count;

				// Override with latest found
				hierarchies[assemblyName] = roleMap;
				allRoles[assemblyName] = roles;

			}
		}

		logger.LogScanningCompleted(roleCount, relationshipCount, hierarchies.Count);

		return new RoleDefinitionScanResult(allRoles, hierarchies);
	}


	private static bool IsRoleDefinitionProvider(Type type) =>
		type != null &&
		type.IsClass &&
		!type.IsAbstract &&
		typeof(IRoleDefinitionProvider).IsAssignableFrom(type);

	private static bool TryScanType(
		Type type,
		ILogger logger,
		[NotNullWhen(true)] out HashSet<Role>? roles,
		[NotNullWhen(true)] out Dictionary<Role, Role[]>? hierarchy) {

		roles = null;
		hierarchy = null;

		try {
			var registeredRoles = type.GetStaticPropertyValue<Role[]>(nameof(IRoleDefinitionProvider.Roles));
			hierarchy = type.GetStaticPropertyValue<Dictionary<Role, Role[]>>(nameof(IRoleDefinitionProvider.RoleHierarchy));

			// Filter out any app roles from RegisteredRoles with a warning
			var validCustomRoles = registeredRoles.Where(r => !r.IsApplicationRole).ToArray();
			var invalidAppRoles = registeredRoles.Where(r => r.IsApplicationRole).ToArray();

			if (invalidAppRoles.Length > 0) {
				var asms = string.Join(", ", invalidAppRoles.Select(r => r.ToString()));
				logger.LogApplicationRolesIgnored(type.FullName, asms);
			}

			// Validate all custom roles in hierarchy exist in the filtered registered set
			var registeredRoleSet = validCustomRoles.ToHashSet();
			var customRolesInHierarchy = hierarchy.Keys
				.Concat(hierarchy.Values.SelectMany(x => x))
				.Where(r => !r.IsApplicationRole);
			var missingCustomRoles = customRolesInHierarchy
				.Where(r => !registeredRoleSet.Contains(r))
				.ToArray();

			if (missingCustomRoles.Length > 0) {
				var missingRoles = string.Join(", ", registeredRoleSet.Select(r => r.ToString()));
				logger.LogUnregisteredRolesInHierarchy(type.FullName, missingRoles);
				return false;
			}

			// All validation passed - return the valid custom roles
			roles = registeredRoleSet;
			logger.LogRolesFound(roles.Count, hierarchy.Count, type.FullName);

			return true;

		} catch (MissingMemberException ex) {
			logger.LogMissingProperties(ex, type.FullName);
			return false;
		} catch (TargetInvocationException ex) {
			logger.LogPropertyAccessError(ex, type.FullName, ex.InnerException?.Message);
			return false;
		} catch (InvalidCastException ex) {
			logger.LogInvalidPropertyTypes(ex, type.FullName);
			return false;
		} catch (Exception ex) {
			logger.LogUnexpectedScanError(ex, type.FullName);
			return false;
		}

	}

}