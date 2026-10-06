using System.ComponentModel;
using ModelContextProtocol.Server;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Services;
using Sannel.Encoding.Manager.Web.Features.Interlace.Services;
using Sannel.Encoding.Manager.Web.Features.Mcp.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Tools;

/// <summary>MCP tools mirroring the Filesystem Browser.</summary>
[McpServerToolType]
public class FilesystemTools
{
	private readonly IFilesystemService _filesystemService;
	private readonly IInterlaceService _interlace;

	public FilesystemTools(IFilesystemService filesystemService, IInterlaceService interlace)
	{
		this._filesystemService = filesystemService;
		this._interlace = interlace;
	}

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
	[Description("""
		Recursively lists every media file under a folder, sorted the same way the Scan page shows them. Use the returned
		relativePath values as sourceRelativePath when queuing with selection "folder".
		Each file has an interlace verdict and a recommendedPreset (checked with ffmpeg, cached). Uncached files report
		"pending" while they are checked in the background: wait about 20 seconds and call this tool again until none are
		pending, then use each file's recommendedPreset as that track's presetLabel.
		""")]
	public Task<IReadOnlyList<McpFileDto>> ListFolderMediaFilesAsync(
		[Description("Root label from list_roots.")] string root,
		[Description("Root-relative folder path. Omit or \"\" for the root itself.")] string? path,
		CancellationToken ct) =>
		McpToolHelpers.GuardAsync<IReadOnlyList<McpFileDto>>(async () =>
		{
			var normalized = McpToolHelpers.NormalizePath(path);
			var files = (await this._filesystemService.GetMediaFilesRecursiveAsync(root, normalized.Length == 0 ? null : normalized, ct))
				.OrderBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
				.ToList();
			var folder = this._filesystemService.ResolvePhysicalPath(root, normalized);
			var physical = files.Select(f => Path.GetFullPath(Path.Combine(folder, f.RelativePath))).ToList();
			var verdicts = await this._interlace.GetFilesAsync(physical, ct);
			return files
				.Select((f, i) =>
				{
					var verdict = verdicts.GetValueOrDefault(physical[i]);
					return new McpFileDto
					{
						Name = f.Name,
						RelativePath = f.RelativePath,
						SizeBytes = f.SizeBytes,
						Interlace = verdict is null ? null : InterlaceMapping.ToMcp(verdict.Verdict),
						InterlacedPercent = verdict?.InterlacedPercent,
						TelecinePercent = verdict?.TelecinePercent,
						RecommendedPreset = verdict?.RecommendedPreset,
					};
				})
				.ToList();
		});
}
