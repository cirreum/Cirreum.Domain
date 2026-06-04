namespace Cirreum.Authorization;

using Microsoft.Extensions.Logging;

/// <summary>
/// Structured logging extensions for RoleDefinitionScanner
/// </summary>
internal static partial class RoleDefinitionScannerLoggerExtensions {

	[LoggerMessage(
		EventId = 1001,
		Level = LogLevel.Debug,
		Message = "Scanning assemblies for role definitions...")]
	public static partial void LogScanningAssemblies(this ILogger logger);

	[LoggerMessage(
		EventId = 1002,
		Level = LogLevel.Warning,
		Message = "Missing assembly Name for assembly '{Assembly}' and Type '{AssemblyQualifiedName}', and will be skipped.")]
	public static partial void LogMissingAssemblyName(
		this ILogger logger,
		string assembly,
		string? assemblyQualifiedName);

	[LoggerMessage(
		EventId = 1003,
		Level = LogLevel.Warning,
		Message = "Found additional implementation in assembly {AssemblyName} for Type {AssemblyQualifiedName}. Using last implementation found.")]
	public static partial void LogAdditionalImplementation(
		this ILogger logger,
		string assemblyName,
		string? assemblyQualifiedName);

	[LoggerMessage(
		EventId = 1004,
		Level = LogLevel.Debug,
		Message = "Completed scanning. Using {RoleCount} custom role(s) and {RelationshipCount} role relationship(s) definition(s) from {AssemblyCount} assemblies")]
	public static partial void LogScanningCompleted(
		this ILogger logger,
		int roleCount,
		int relationshipCount,
		int assemblyCount);

	[LoggerMessage(
		EventId = 1005,
		Level = LogLevel.Warning,
		Message = "Role definition warning in {TypeName}: Application roles in RegisteredRoles will be ignored (already registered by system): {InvalidRoles}")]
	public static partial void LogApplicationRolesIgnored(
		this ILogger logger,
		string? typeName,
		string invalidRoles);

	[LoggerMessage(
		EventId = 1006,
		Level = LogLevel.Error,
		Message = "Role definition error in {TypeName}: Hierarchy references unregistered custom roles: {MissingRoles}")]
	public static partial void LogUnregisteredRolesInHierarchy(
		this ILogger logger,
		string? typeName,
		string missingRoles);

	[LoggerMessage(
		EventId = 1007,
		Level = LogLevel.Debug,
		Message = "Found {RoleCount} custom roles and {HierarchyCount} role relationships in {TypeName}")]
	public static partial void LogRolesFound(
		this ILogger logger,
		int roleCount,
		int hierarchyCount,
		string? typeName);

	[LoggerMessage(
		EventId = 1008,
		Level = LogLevel.Error,
		Message = "Missing RegisteredRoles or RoleHierarchy property in {TypeName}")]
	public static partial void LogMissingProperties(
		this ILogger logger,
		Exception ex,
		string? typeName);

	[LoggerMessage(
		EventId = 1009,
		Level = LogLevel.Error,
		Message = "Error accessing properties in {TypeName}: {InnerMessage}")]
	public static partial void LogPropertyAccessError(
		this ILogger logger,
		Exception ex,
		string? typeName,
		string? innerMessage);

	[LoggerMessage(
		EventId = 1010,
		Level = LogLevel.Error,
		Message = "Properties in {TypeName} are not of expected types (Role[] and Dictionary<Role, Role[]>)")]
	public static partial void LogInvalidPropertyTypes(
		this ILogger logger,
		Exception ex,
		string? typeName);

	[LoggerMessage(
		EventId = 1011,
		Level = LogLevel.Error,
		Message = "Unexpected error scanning type {TypeName}")]
	public static partial void LogUnexpectedScanError(
		this ILogger logger,
		Exception ex,
		string? typeName);

}