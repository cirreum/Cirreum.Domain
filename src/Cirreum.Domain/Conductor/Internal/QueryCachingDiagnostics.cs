namespace Cirreum.Conductor.Internal;

using Cirreum.Caching;
using Microsoft.Extensions.Logging;

internal static class QueryCachingDiagnostics {

	private static int _warned;

	public static void WarnIfMisconfigured(ILogger logger, CacheSettings settings, ICacheService cache) {

		if (settings.Provider is not (CacheProvider.Hybrid or CacheProvider.Distributed)) {
			return;
		}

		if (cache is not NoCacheService) {
			return;
		}

		if (Interlocked.Exchange(ref _warned, 1) != 0) {
			return;   // once per process, thread-safe
		}

		logger.LogWarning(
			"CacheProvider is '{Provider}' but the query-caching service resolved to NoCacheService. " +
			"Ensure the {Provider} cache package is registered — and registered before AddDomainServices/AddCirreumCaching.",
			settings.Provider, settings.Provider);
	}
}