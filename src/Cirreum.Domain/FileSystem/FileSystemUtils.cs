namespace Cirreum.FileSystem;

/// <summary>
/// Helper utilities for file system services.
/// </summary>
public static class FileSystemUtils {

	/// <summary>
	/// The default search pattern: * (star)
	/// </summary>
	public const string DefaultSearchPattern = "*";

	/// <summary>
	/// Validates and normalizes search patterns, ensuring consistent behavior.
	/// </summary>
	/// <param name="patterns">Single pattern or enumerable of patterns</param>
	/// <returns>Normalized collection of search patterns</returns>
	public static IEnumerable<string> NormalizeSearchPatterns(params string?[] patterns) {
		if (patterns.Length == 0) {
			return [DefaultSearchPattern];
		}

		var normalized = patterns
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.Select(p => p!.Trim()); // p! is safe here due to Where clause

		return normalized.Any() ? normalized : [DefaultSearchPattern];
	}
}