namespace Cirreum.Conductor.Internal;

using System.Collections.Concurrent;

/// <summary>
/// Manages wrapper instance caches for Conductor.
/// </summary>
/// <remarks>
/// <para>
/// These caches are intentionally unbounded because:
/// <list type="bullet">
/// <item>Operation/notification types are static and don't change at runtime</item>
/// <item>Even with 1000+ types, memory usage is negligible (~48KB)</item>
/// <item>Wrapper instances are lightweight (just type metadata)</item>
/// <item>Cache size is naturally bounded by the number of types in your application</item>
/// </list>
/// </para>
/// <para>
/// Caches are scoped to Conductor to avoid namespace pollution — operation types used
/// as cache keys here won't collide with other subsystems using the same types.
/// </para>
/// <para>
/// For development scenarios with hot reload, use <see cref="ClearAll"/> to reset caches.
/// </para>
/// </remarks>
internal static class TypeCache {

	/// <summary>
	/// Cache for void operation handlers (IOperation with no return value).
	/// </summary>
	internal static readonly ConcurrentDictionary<Type, OperationHandlerWrapper> VoidHandlers = new();

	/// <summary>
	/// Cache for typed operation handlers (IOperation&lt;TResultValue&gt;).
	/// Stores as object because different TResultValue types share the same cache.
	/// </summary>
	internal static readonly ConcurrentDictionary<Type, object> ResponseHandlers = new();

	/// <summary>
	/// Cache for notification handler wrappers.
	/// </summary>
	internal static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> NotificationHandlers = new();

	/// <summary>
	/// Clears all Conductor wrapper caches. Use only in hot-reload scenarios.
	/// </summary>
	public static void ClearAll() {
		VoidHandlers.Clear();
		ResponseHandlers.Clear();
		NotificationHandlers.Clear();
	}

	/// <summary>
	/// Gets diagnostics about Conductor cache state.
	/// </summary>
	public static CacheStatistics GetStatistics() => new(
		VoidHandlerCount: VoidHandlers.Count,
		ResponseHandlerCount: ResponseHandlers.Count,
		NotificationHandlerCount: NotificationHandlers.Count
	);
}

/// <summary>
/// Statistics about Conductor cache state.
/// </summary>
public readonly record struct CacheStatistics(
	int VoidHandlerCount,
	int ResponseHandlerCount,
	int NotificationHandlerCount) {

	/// <summary>
	/// Gets the total number of cached wrappers across all caches.
	/// </summary>
	public int TotalCount => this.VoidHandlerCount + this.ResponseHandlerCount + this.NotificationHandlerCount;

	/// <summary>
	/// Gets the approximate memory usage of cached wrappers in bytes.
	/// This is a rough estimate assuming ~48 bytes per wrapper instance.
	/// </summary>
	public long EstimatedMemoryBytes => this.TotalCount * 48L;
}