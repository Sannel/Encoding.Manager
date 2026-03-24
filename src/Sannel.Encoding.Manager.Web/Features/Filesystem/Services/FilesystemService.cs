using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Options;

namespace Sannel.Encoding.Manager.Web.Features.Filesystem.Services;

/// <summary>
/// Implementation of filesystem browsing and management service.
/// </summary>
public class FilesystemService : IFilesystemService
{
	private readonly FilesystemOptions _options;
	private readonly ILogger<FilesystemService> _logger;

	private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".mp4",
		".mkv"
	};

	public FilesystemService(IOptions<FilesystemOptions> options, ILogger<FilesystemService> logger)
	{
		_options = options.Value;
		_logger = logger;
	}

	/// <inheritdoc />
	public Task<IEnumerable<ConfiguredDirectoryResponse>> GetConfiguredDirectoriesAsync(CancellationToken cancellationToken = default)
	{
		var results = _options.Roots.Select(root => new ConfiguredDirectoryResponse
		{
			Label = root.Label,
			Exists = Directory.Exists(root.Path)
		});

		return Task.FromResult(results);
	}

	/// <inheritdoc />
	public Task<BrowseResponse> BrowseAsync(string label, string? relativePath = null, CancellationToken cancellationToken = default)
	{
		// Find the root configuration by label
		var rootConfig = _options.Roots.FirstOrDefault(r => r.Label.Equals(label, StringComparison.OrdinalIgnoreCase));
		if (rootConfig == null)
		{
			throw new ArgumentException($"Invalid label: '{label}'", nameof(label));
		}

		// Get the canonical root path
		var canonicalRootPath = Path.GetFullPath(rootConfig.Path);

		// Construct the target path
		var targetPath = string.IsNullOrWhiteSpace(relativePath)
			? canonicalRootPath
			: Path.Combine(canonicalRootPath, relativePath);

		// Canonicalize the target path to prevent symlink/traversal attacks
		var canonicalTargetPath = Path.GetFullPath(targetPath);

		// Ensure the target path is within the root boundary
		if (!IsPathWithinRoot(canonicalTargetPath, canonicalRootPath))
		{
			throw new ArgumentException($"Path escapes root boundary: '{relativePath}'", nameof(relativePath));
		}

		// Check if the directory exists
		if (!Directory.Exists(canonicalTargetPath))
		{
			throw new DirectoryNotFoundException($"Directory not found: '{relativePath ?? "/"}'");
		}

		// Get immediate subdirectories
		var directories = Directory.GetDirectories(canonicalTargetPath)
			.Select(dir => new DirectoryInfo(dir))
			.Select(dirInfo => new DirectoryEntryResponse
			{
				Name = dirInfo.Name,
				DiscType = DetectDiscType(dirInfo.FullName)
			})
			.OrderBy(d => d.Name)
			.ToList();

		// Get media files (.mp4, .mkv)
		var files = Directory.GetFiles(canonicalTargetPath)
			.Select(file => new FileInfo(file))
			.Where(fileInfo => MediaExtensions.Contains(fileInfo.Extension))
			.Select(fileInfo => new FileEntryResponse
			{
				Name = fileInfo.Name,
				SizeBytes = fileInfo.Length
			})
			.OrderBy(f => f.Name)
			.ToList();

		return Task.FromResult(new BrowseResponse
		{
			Directories = directories,
			Files = files
		});
	}

	/// <summary>
	/// Checks if a path is within a root boundary.
	/// </summary>
	private static bool IsPathWithinRoot(string path, string root)
	{
		// Both paths should already be canonicalized
		// Ensure the path starts with the root and has a directory separator
		return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
			   (path.Length == root.Length || path[root.Length] == Path.DirectorySeparatorChar || path[root.Length] == Path.AltDirectorySeparatorChar);
	}

	/// <inheritdoc />
	public string ResolvePhysicalPath(string label, string relativePath)
	{
		if (string.IsNullOrWhiteSpace(label))
		{
			throw new ArgumentException("Label must not be empty.", nameof(label));
		}

		if (string.IsNullOrWhiteSpace(relativePath))
		{
			throw new ArgumentException("Relative path must not be empty.", nameof(relativePath));
		}

		// Reject path segments that attempt traversal
		var segments = relativePath.Split('/', '\\');
		foreach (var segment in segments)
		{
			var trimmed = segment.Trim();
			if (trimmed is ".." or "." || trimmed.Length == 0)
			{
				throw new ArgumentException("Invalid path: the specified path is not allowed.", nameof(relativePath));
			}
		}

		var rootConfig = _options.Roots.FirstOrDefault(r => r.Label.Equals(label, StringComparison.OrdinalIgnoreCase))
			?? throw new ArgumentException($"Invalid label: '{label}'", nameof(label));

		var canonicalRootPath = Path.GetFullPath(rootConfig.Path);
		var combinedPath = Path.Combine(canonicalRootPath, relativePath);
		var canonicalPath = Path.GetFullPath(combinedPath);

		if (!IsPathWithinRoot(canonicalPath, canonicalRootPath))
		{
			throw new ArgumentException("Invalid path: the specified path is not allowed.", nameof(relativePath));
		}

		return canonicalPath;
	}

	/// <summary>
	/// Detects the disc type (Blu-ray or DVD) for a directory.
	/// </summary>
	private DiscType DetectDiscType(string directoryPath)
	{
		try
		{
			// Check for Blu-ray structure
			var bdmvPath = Path.Combine(directoryPath, "BDMV");
			if (Directory.Exists(bdmvPath))
			{
				// Check for index.bdmv
				if (File.Exists(Path.Combine(bdmvPath, "index.bdmv")))
				{
					return DiscType.BluRay;
				}

				// Check for STREAM folder
				if (Directory.Exists(Path.Combine(bdmvPath, "STREAM")))
				{
					return DiscType.BluRay;
				}
			}

			// Check for DVD structure
			var videoTsPath = Path.Combine(directoryPath, "VIDEO_TS");
			if (Directory.Exists(videoTsPath))
			{
				// Check for VIDEO_TS.IFO
				if (File.Exists(Path.Combine(videoTsPath, "VIDEO_TS.IFO")))
				{
					return DiscType.DVD;
				}
			}

			return DiscType.None;
		}
		catch (Exception ex)
		{
			// Log and return None if we can't determine the type
			_logger.LogWarning(ex, "Failed to detect disc type for directory: {DirectoryPath}", directoryPath);
			return DiscType.None;
		}
	}
}
