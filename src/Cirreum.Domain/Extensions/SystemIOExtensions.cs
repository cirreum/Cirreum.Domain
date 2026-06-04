namespace System.IO;

using Cirreum.FileSystem;
using System;
using System.Linq;
using System.Text;

/// <summary>
/// System.IO Extension Helpers.
/// </summary>
public static class SystemIOExtensions {

	/// <summary>
	/// Determines if the given path represents a directory, file, or does not exist.
	/// </summary>
	/// <param name="path">The path to check.</param>
	/// <returns>
	/// A <see cref="PathType"/> indicating whether the path is a directory, file, or does not exist.
	/// </returns>
	/// <remarks>
	/// This method uses a single filesystem call to determine the path type, making it more efficient
	/// than separate existence checks for files and directories.
	/// </remarks>
	public static PathType GetPathType(this string path) {

		if (string.IsNullOrWhiteSpace(path)) {
			return PathType.NotFound;
		}

		try {

			// Get the file attributes for file or directory in a single call
			var fileAttr = File.GetAttributes(path);

			// Check if it's a directory
			if (fileAttr.HasFlag(FileAttributes.Directory)) {
				return PathType.Directory;
			}

			return PathType.File;

		} catch (FileNotFoundException) {
			return PathType.NotFound;
		} catch (DirectoryNotFoundException) {
			return PathType.NotFound;
		} catch (UnauthorizedAccessException) {
			// You might want to handle this differently or create a specific enum value
			return PathType.NotFound;
		}

	}

	/// <summary>
	/// Replaces characters in <paramref name="value"/> that are not allowed in 
	/// file names with the specified <paramref name="replacement"/> character.
	/// </summary>
	/// <param name="value">Text to make into a valid filename. The same string is returned if it is valid already.</param>
	/// <param name="replacement">Replacement character, or null to simply remove bad characters. Defaults to an underscore '_'</param>
	/// <param name="fancy">Whether to replace quotes and slashes with the non-ASCII characters ” and ⁄.</param>
	/// <returns>A string that can be used as a filename.</returns>
	/// <remarks>
	/// If the output string would otherwise be empty, returns "_"
	/// </remarks>
	public static string ToValidFileName(this string value, char? replacement = '_', bool fancy = true) {

		var sb = new StringBuilder(value.Length);
		var invalids = Path.GetInvalidFileNameChars();
		var changed = false;
		for (var i = 0; i < value.Length; i++) {
			var c = value[i];
			if (invalids.Contains(c)) {
				changed = true;
				var repl = replacement ?? '\0';
				if (fancy) {
					if (c == '"') {
						repl = '”'; // U+201D right double quotation mark
					} else if (c == '\'') {
						repl = '’'; // U+2019 right single quotation mark
					} else if (c == '/') {
						repl = '⁄'; // U+2044 fraction slash
					}
				}
				if (repl != '\0') {
					sb.Append(repl);
				}
			} else {
				sb.Append(c);
			}
		}

		if (sb.Length == 0) {
			return "_";
		}

		return changed ? sb.ToString() : value;

	}

	/// <summary>
	/// Determines if the specified 'fileName' contains any invalid File Name characeters.
	/// </summary>
	/// <param name="fileName">The file name to evaluate.</param>
	/// <returns><see langword="true"/> if the specified 'fileName' does not contain any invalid file name characters; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// This method simply compares each character against the characters returned from <see cref="Path.GetInvalidFileNameChars"/>.
	/// </remarks>
	public static bool IsValidFileName(this string fileName) {
		return !fileName.ToCharArray().Any(c => Path.GetInvalidFileNameChars().Contains(c));
	}

	/// <summary>
	/// Determines if the specified 'pathString' contains any invalid Path characeters.
	/// </summary>
	/// <param name="pathString">The path to evaluate.</param>
	/// <returns><see langword="true"/> if the specified 'pathString' does not contain any invalid path characters; otherwise <see langword="false"/>.</returns>
	/// <remarks>
	/// This method simply compares each character against the characters returned from <see cref="Path.GetInvalidPathChars"/>.
	/// </remarks>
	public static bool IsValidPath(this string pathString) {
		return !pathString.ToCharArray().Any(c => Path.GetInvalidPathChars().Contains(c));
	}

	/// <summary>
	/// Ensure the specified string <paramref name="value"/> is not longer than the
	/// specified <paramref name="maxLength"/>, truncating if necessary.
	/// </summary>
	/// <param name="value">The string to evaluate.</param>
	/// <param name="maxLength">The maximum allowed length.</param>
	/// <returns>The original value or the truncated value if the original value exceeded the maxLength.</returns>
	public static string EnsureMaxLength(this string value, int maxLength) {
		return string.IsNullOrEmpty(value) ?
			value :
			value.Length <= maxLength ?
			value :
			value[..maxLength];
	}

	/// <summary>
	/// Ensures the specified string ends with a slash ('/').
	/// </summary>
	/// <param name="value">The string to evaluate.</param>
	/// <returns>A string that ends with a slash ('/')</returns>
	public static string EnsureTrailingSlash(this string value) {
		return value.EndsWith('/') ?
			value :
			value + "/";
	}

	/// <summary>
	/// Ensures the specified string starts with a slash ('/').
	/// </summary>
	/// <param name="value">The string to evaluate.</param>
	/// <returns>A string that starts with a slash ('/')</returns>
	public static string EnsureStartingSlash(this string value) {
		return value.StartsWith('/') ?
			value :
			$"/{value}";
	}

	/// <summary>
	/// Removes all trailing slashes ('/') from the <paramref name="value"/>.
	/// </summary>
	/// <param name="value">The string to evaluate.</param>
	/// <returns>A string that does not end with any trailing slashes.</returns>
	public static string EnsureNoTrailingSlash(this string value) {
		while (value.EndsWith('/')) {
			value = value[..^1];
		}
		return value;
	}

}