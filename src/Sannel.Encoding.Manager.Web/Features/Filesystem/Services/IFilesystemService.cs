using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Filesystem.Services;

/// <summary>
/// Service for browsing and managing filesystem operations.
/// </summary>
public interface IFilesystemService
{
	/// <summary>
	/// Gets the list of configured root directories.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A collection of configured directory information.</returns>
	Task<IEnumerable<ConfiguredDirectoryResponse>> GetConfiguredDirectoriesAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Browses a directory and returns its immediate children (subdirectories and media files).
	/// </summary>
	/// <param name="label">The label of the configured root directory.</param>
	/// <param name="relativePath">Optional relative path within the root directory.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>A browse response containing directories and files.</returns>
	/// <exception cref="ArgumentException">Thrown when the label is invalid or the path escapes the root boundary.</exception>
	/// <exception cref="DirectoryNotFoundException">Thrown when the requested directory does not exist.</exception>
	Task<BrowseResponse> BrowseAsync(string label, string? relativePath = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Browses a directory and returns its immediate children filtered by the given file extensions.
	/// Useful for browsing for non-media files such as HandBrake preset .json files.
	/// </summary>
	Task<BrowseResponse> BrowseWithExtensionFilterAsync(string label, string? relativePath, string[] fileExtensions, CancellationToken cancellationToken = default);

	/// <summary>
	/// Finds all media files under a folder recursively and returns them relative to that folder.
	/// </summary>
	Task<IReadOnlyList<FileEntryResponse>> GetMediaFilesRecursiveAsync(string label, string? relativePath = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Resolves a root label and relative path to an absolute physical path,
	/// with path-traversal validation.
	/// </summary>
	/// <param name="label">The label of the configured root directory.</param>
	/// <param name="relativePath">The relative path within the root directory.</param>
	/// <returns>The resolved absolute physical path.</returns>
	/// <exception cref="ArgumentException">Thrown when the label is invalid or the path escapes the root boundary.</exception>
	string ResolvePhysicalPath(string label, string relativePath);
}
