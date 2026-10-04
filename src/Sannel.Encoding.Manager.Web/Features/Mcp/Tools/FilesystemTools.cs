using System.ComponentModel;
using ModelContextProtocol.Server;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Services;
using Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Tools;

/// <summary>MCP tools mirroring the Filesystem Browser.</summary>
[McpServerToolType]
public class FilesystemTools
{
	private readonly IFilesystemService _filesystemService;

	public FilesystemTools(IFilesystemService filesystemService) =>
		this._filesystemService = filesystemService;

	[McpServerTool(Name = "list_roots", ReadOnly = true, Idempotent = true)]
	[Description("Lists the configured media root directories. Every other tool addresses files by a root label plus a root-relative path.")]
	public async Task<IReadOnlyList<ConfiguredDirectoryResponse>> ListRootsAsync(CancellationToken ct) =>
		(await this._filesystemService.GetConfiguredDirectoriesAsync(ct)).ToList();

	[McpServerTool(Name = "browse_directory", ReadOnly = true, Idempotent = true)]
	[Description("Lists the immediate subdirectories and media files (.mkv, .mp4) of a folder. Directories with discType DVD or BluRay are disc rips: select them as a whole (selection \"disc\") and scan them with scan_disc instead of opening them.")]
	public Task<McpBrowseResult> BrowseDirectoryAsync(
		[Description("Root label from list_roots.")] string root,
		[Description("Root-relative folder path using forward slashes. Omit or \"\" for the root itself.")] string? path,
		CancellationToken ct) =>
		McpToolHelpers.GuardAsync(async () =>
		{
			var normalized = McpToolHelpers.NormalizePath(path);
			var result = await this._filesystemService.BrowseAsync(root, normalized.Length == 0 ? null : normalized, ct);
			return new McpBrowseResult
			{
				Root = root,
				Path = normalized,
				Directories = result.Directories
					.Select(d => new McpDirectoryDto { Name = d.Name, DiscType = d.DiscType.ToString() })
					.ToList(),
				Files = result.Files
					.Select(f => new McpFileDto { Name = f.Name, RelativePath = f.Name, SizeBytes = f.SizeBytes })
					.ToList(),
			};
		});

	[McpServerTool(Name = "list_folder_media_files", ReadOnly = true, Idempotent = true)]
	[Description("Recursively lists every media file under a folder, sorted the same way the Scan page shows them. Use the returned relativePath values as sourceRelativePath when queuing with selection \"folder\".")]
	public Task<IReadOnlyList<McpFileDto>> ListFolderMediaFilesAsync(
		[Description("Root label from list_roots.")] string root,
		[Description("Root-relative folder path. Omit or \"\" for the root itself.")] string? path,
		CancellationToken ct) =>
		McpToolHelpers.GuardAsync<IReadOnlyList<McpFileDto>>(async () =>
		{
			var normalized = McpToolHelpers.NormalizePath(path);
			var files = await this._filesystemService.GetMediaFilesRecursiveAsync(root, normalized.Length == 0 ? null : normalized, ct);
			return files
				.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
				.Select(f => new McpFileDto { Name = f.Name, RelativePath = f.RelativePath, SizeBytes = f.SizeBytes })
				.ToList();
		});
}
