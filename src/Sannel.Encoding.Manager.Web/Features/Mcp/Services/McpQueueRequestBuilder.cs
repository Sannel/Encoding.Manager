using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Services;
using Sannel.Encoding.Manager.Web.Features.Mcp.Dto;
using Sannel.Encoding.Manager.Web.Features.Mcp.Options;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;
using Sannel.Encoding.Manager.Web.Features.Scan.Dto;
using Sannel.Encoding.Manager.Web.Features.Scan.Services;
using Sannel.Encoding.Manager.Web.Features.Scan.Utilities;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Services;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Services;

/// <summary>
/// Validates a <c>queue_encode_job</c> request and turns it into an <see cref="EncodeJobSubmission"/>
/// shaped exactly like one the Scan page would produce. Collects every problem rather than stopping at the first.
/// </summary>
public partial class McpQueueRequestBuilder
{
	private static readonly char[] _invalidNameChars = [.. Path.GetInvalidFileNameChars(), '/', '\\', ':', '*', '?', '"', '<', '>', '|'];

	private readonly IFilesystemService _filesystemService;
	private readonly IBackgroundScanCoordinator _scanCoordinator;
	private readonly IPresetService _presetService;
	private readonly ITvdbService _tvdbService;
	private readonly McpOptions _options;

	public McpQueueRequestBuilder(
		IFilesystemService filesystemService,
		IBackgroundScanCoordinator scanCoordinator,
		IPresetService presetService,
		ITvdbService tvdbService,
		IOptions<McpOptions> options)
	{
		this._filesystemService = filesystemService;
		this._scanCoordinator = scanCoordinator;
		this._presetService = presetService;
		this._tvdbService = tvdbService;
		this._options = options.Value;
	}

	/// <summary>Builds the submission, or returns the list of validation errors.</summary>
	public async Task<(EncodeJobSubmission? Submission, IReadOnlyList<string> Errors)> BuildAsync(
		McpQueueEncodeRequest request,
		string? createdBy,
		string? createdByObjectId,
		CancellationToken ct)
	{
		var errors = new List<string>();
		var path = NormalizePath(request.Path);
		var selection = (request.Selection ?? string.Empty).Trim().ToLowerInvariant();

		if (string.IsNullOrWhiteSpace(request.Root))
		{
			errors.Add("root is required.");
			return (null, errors);
		}

		string physical;
		try
		{
			physical = this._filesystemService.ResolvePhysicalPath(request.Root, path);
		}
		catch (ArgumentException ex)
		{
			errors.Add($"Invalid root or path: {ex.Message}");
			return (null, errors);
		}

		if (request.Tracks.Count == 0)
		{
			errors.Add("tracks must contain at least one track.");
		}

		string discPath;
		string mode;
		List<EncodeTrackConfig> tracks;

		switch (selection)
		{
			case "disc":
				(discPath, mode, tracks) = await this.BuildDiscAsync(request, path, physical, errors, ct);
				break;
			case "folder":
				(discPath, mode, tracks) = await this.BuildFolderAsync(request, path, physical, errors, ct);
				break;
			case "file":
				(discPath, mode, tracks) = BuildFile(request, path, physical, errors);
				break;
			default:
				errors.Add("selection must be \"disc\", \"folder\" or \"file\".");
				return (null, errors);
		}

		await this.ValidateCommonAsync(request, errors, ct);

		string? showName = null;
		if (request.TvdbSeriesId is { } seriesId && errors.Count == 0)
		{
			try
			{
				showName = await this._tvdbService.GetSeriesNameAsync(seriesId, ct);
				if (showName is null)
				{
					errors.Add($"TVDB series {seriesId} was not found.");
				}
			}
			catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
			{
				errors.Add($"TVDB lookup for series {seriesId} failed: {ex.Message}");
			}
		}

		if (errors.Count > 0)
		{
			return (null, errors);
		}

		return (new EncodeJobSubmission
		{
			RootLabel = request.Root,
			DiscPath = discPath,
			Mode = mode,
			PresetLabel = string.IsNullOrWhiteSpace(request.PresetLabel) ? null : request.PresetLabel.Trim(),
			TvdbId = request.TvdbSeriesId,
			TvdbShowName = showName,
			Tracks = tracks,
			CreatedBy = createdBy,
			CreatedByObjectId = createdByObjectId,
			CreatedVia = "MCP",
		}, errors);
	}

	private async Task<(string DiscPath, string Mode, List<EncodeTrackConfig> Tracks)> BuildDiscAsync(
		McpQueueEncodeRequest request, string path, string physical, List<string> errors, CancellationToken ct)
	{
		var mode = string.Equals(request.Mode, "Chapters", StringComparison.OrdinalIgnoreCase) ? "Chapters"
			: string.Equals(request.Mode, "Titles", StringComparison.OrdinalIgnoreCase) ? "Titles"
			: null;
		if (mode is null)
		{
			errors.Add("mode must be \"Titles\" or \"Chapters\" for selection \"disc\".");
			mode = "Titles";
		}

		if (path.Length == 0 || !Directory.Exists(physical))
		{
			errors.Add("path must be an existing disc folder for selection \"disc\".");
			return (path, mode, []);
		}

		var status = this._scanCoordinator.GetStatus(request.Root, path)
			?? await this._scanCoordinator.StartOrGetAsync(request.Root, path, false, TimeSpan.FromSeconds(Math.Max(0, this._options.ScanWaitSeconds)), ct);
		if (status.State != BackgroundJobState.Completed || status.Result is null)
		{
			errors.Add(status.State == BackgroundJobState.Failed
				? $"The disc scan failed ({status.Error}); titles cannot be validated."
				: "The disc has not finished scanning. Call scan_disc (and get_scan_status until Completed), then retry.");
			return (path, mode, []);
		}

		var titles = status.Result.Titles.ToDictionary(t => t.TitleNumber);
		var tracks = new List<EncodeTrackConfig>();
		for (var i = 0; i < request.Tracks.Count; i++)
		{
			var t = request.Tracks[i];
			var label = $"tracks[{i}]";
			if (t.TitleNumber is not { } titleNumber)
			{
				errors.Add($"{label}: titleNumber is required for selection \"disc\".");
				continue;
			}

			if (!titles.TryGetValue(titleNumber, out var title))
			{
				errors.Add($"{label}: title {titleNumber} does not exist on this disc (valid: {string.Join(", ", titles.Keys.Order())}).");
				continue;
			}

			if (mode == "Chapters")
			{
				var chapterCount = title.Chapters.Count;
				if (t.StartChapter is not { } start || t.EndChapter is not { } end)
				{
					errors.Add($"{label}: startChapter and endChapter are required for mode \"Chapters\".");
				}
				else if (start < 1 || end < start || end > chapterCount)
				{
					errors.Add($"{label}: chapters {start}-{end} are out of range for title {titleNumber} (1-{chapterCount}).");
				}
			}
			else if (t.StartChapter is not null || t.EndChapter is not null)
			{
				errors.Add($"{label}: startChapter/endChapter only apply to mode \"Chapters\".");
			}

			tracks.Add(ToConfig(t, titleNumber, null, request.MovieYear, mode == "Chapters"));
		}

		return (path, mode, tracks);
	}

	private async Task<(string DiscPath, string Mode, List<EncodeTrackConfig> Tracks)> BuildFolderAsync(
		McpQueueEncodeRequest request, string path, string physical, List<string> errors, CancellationToken ct)
	{
		if (!Directory.Exists(physical))
		{
			errors.Add("path must be an existing folder for selection \"folder\".");
			return (path, "Files", []);
		}

		var files = await this._filesystemService.GetMediaFilesRecursiveAsync(request.Root, path.Length == 0 ? null : path, ct);
		var known = files.ToDictionary(f => NormalizePath(f.RelativePath), f => f.RelativePath, StringComparer.OrdinalIgnoreCase);
		var tracks = new List<EncodeTrackConfig>();
		for (var i = 0; i < request.Tracks.Count; i++)
		{
			var t = request.Tracks[i];
			var label = $"tracks[{i}]";
			var source = NormalizePath(t.SourceRelativePath);
			if (source.Length == 0)
			{
				errors.Add($"{label}: sourceRelativePath is required for selection \"folder\".");
				continue;
			}

			if (!known.TryGetValue(source, out var actual))
			{
				errors.Add($"{label}: \"{source}\" is not a media file in this folder (see list_folder_media_files).");
				continue;
			}

			if (t.StartChapter is not null || t.EndChapter is not null)
			{
				errors.Add($"{label}: chapters are not supported for files.");
			}

			tracks.Add(ToConfig(t, 1, actual, request.MovieYear, false));
		}

		return (path, "Files", tracks);
	}

	private static (string DiscPath, string Mode, List<EncodeTrackConfig> Tracks) BuildFile(
		McpQueueEncodeRequest request, string path, string physical, List<string> errors)
	{
		if (path.Length == 0 || !File.Exists(physical))
		{
			errors.Add("path must be an existing media file for selection \"file\".");
			return (string.Empty, "Files", []);
		}

		var lastSlash = path.LastIndexOf('/');
		var parent = lastSlash > 0 ? path[..lastSlash] : string.Empty;
		var fileName = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;

		if (request.Tracks.Count != 1)
		{
			errors.Add("selection \"file\" takes exactly one track.");
			return (parent, "Files", []);
		}

		var t = request.Tracks[0];
		if (t.StartChapter is not null || t.EndChapter is not null)
		{
			errors.Add("tracks[0]: chapters are not supported for files.");
		}

		return (parent, "Files", [ToConfig(t, 1, fileName, request.MovieYear, false)]);
	}

	private async Task ValidateCommonAsync(McpQueueEncodeRequest request, List<string> errors, CancellationToken ct)
	{
		if (!string.IsNullOrWhiteSpace(request.PresetLabel))
		{
			var presets = await this._presetService.GetPresetsAsync(ct);
			if (!presets.Any(p => string.Equals(p.Label, request.PresetLabel.Trim(), StringComparison.Ordinal)))
			{
				errors.Add($"presetLabel \"{request.PresetLabel}\" does not exist (see list_presets).");
			}
		}

		if (!string.IsNullOrWhiteSpace(request.MovieYear) && !YearRegex().IsMatch(request.MovieYear.Trim()))
		{
			errors.Add("movieYear must be a four-digit year.");
		}

		var resolutions = ResolutionDetector.GetAvailableResolutions();
		for (var i = 0; i < request.Tracks.Count; i++)
		{
			var t = request.Tracks[i];
			var label = $"tracks[{i}]";
			if (!string.IsNullOrWhiteSpace(t.OutputName) && t.OutputName.IndexOfAny(_invalidNameChars) >= 0)
			{
				errors.Add($"{label}: outputName \"{t.OutputName}\" contains characters that are not allowed in file names.");
			}

			if (!string.IsNullOrWhiteSpace(t.Resolution) && !resolutions.Contains(t.Resolution.Trim()))
			{
				errors.Add($"{label}: resolution must be one of {string.Join(", ", resolutions)}.");
			}

			if (t.SeasonNumber is < 0 || t.EpisodeNumber is < 0)
			{
				errors.Add($"{label}: seasonNumber and episodeNumber cannot be negative.");
			}
		}
	}

	private static EncodeTrackConfig ToConfig(McpQueueTrack track, int titleNumber, string? sourceRelativePath, string? movieYear, bool useChapters) => new()
	{
		TitleNumber = titleNumber,
		StartChapter = useChapters ? track.StartChapter : null,
		EndChapter = useChapters ? track.EndChapter : null,
		SourceRelativePath = sourceRelativePath,
		OutputName = track.OutputName?.Trim() ?? string.Empty,
		SeasonNumber = track.SeasonNumber,
		EpisodeNumber = track.EpisodeNumber,
		MovieYear = string.IsNullOrWhiteSpace(movieYear) ? null : movieYear.Trim(),
		Resolution = string.IsNullOrWhiteSpace(track.Resolution) ? null : track.Resolution.Trim(),
	};

	private static string NormalizePath(string? path) =>
		(path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');

	[GeneratedRegex(@"^\d{4}$")]
	private static partial Regex YearRegex();
}
