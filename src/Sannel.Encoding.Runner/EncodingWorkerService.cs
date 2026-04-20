using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Runner.Features.Encoding;
using Sannel.Encoding.Runner.Features.Runner.Dto;
using Sannel.Encoding.Runner.Features.Runner.Options;
using Sannel.Encoding.Runner.Features.Runner.Services;

namespace Sannel.Encoding.Runner;

public class EncodingWorkerService : BackgroundService
{
	private readonly IRunnerApiClient _api;
	private readonly IHandBrakeService _handBrake;
	private readonly PathNormalizer _pathNormalizer;
	private readonly RunnerOptions _runnerOptions;
	private readonly FilesystemOptions _fsOptions;
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly ILogger<EncodingWorkerService> _logger;
	private static readonly TimeSpan CancelPollInterval = TimeSpan.FromSeconds(2);

	public EncodingWorkerService(
		IRunnerApiClient api,
		IHandBrakeService handBrake,
		PathNormalizer pathNormalizer,
		IOptions<RunnerOptions> runnerOptions,
		IOptions<FilesystemOptions> fsOptions,
		IHttpClientFactory httpClientFactory,
		ILogger<EncodingWorkerService> logger)
	{
		_api = api;
		_handBrake = handBrake;
		_pathNormalizer = pathNormalizer;
		_runnerOptions = runnerOptions.Value;
		_fsOptions = fsOptions.Value;
		_httpClientFactory = httpClientFactory;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_logger.LogInformation("Encoding worker '{Name}' starting.", _runnerOptions.Name);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await _api.SendHeartbeatAsync(_runnerOptions.Name, stoppingToken);

				var enabled = await _api.IsEnabledAsync(_runnerOptions.Name, stoppingToken);
				if (!enabled)
				{
					_logger.LogInformation("Runner '{Name}' is disabled, sleeping.", _runnerOptions.Name);
					await DelayForPollIntervalAsync(stoppingToken);
					continue;
				}

				var job = await _api.ClaimNextJobAsync(_runnerOptions.Name, stoppingToken);
				if (job is null)
				{
					_logger.LogDebug("No jobs available, sleeping.");
					await DelayForPollIntervalAsync(stoppingToken);
					continue;
				}

				_logger.LogInformation("Claimed job {JobId} for disc '{DiscPath}'.", job.JobId, job.DiscPath);
				await ProcessJobAsync(job, stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error in encoding worker loop.");
				await DelayForPollIntervalAsync(stoppingToken);
			}
		}

		_logger.LogInformation("Encoding worker '{Name}' stopping.", _runnerOptions.Name);
	}

	private async Task DelayForPollIntervalAsync(CancellationToken ct)
	{
		var pollSeconds = _runnerOptions.PollIntervalSeconds;
		if (pollSeconds < 1)
		{
			_logger.LogWarning(
				"Runner poll interval '{PollIntervalSeconds}' is invalid; using 1 second.",
				pollSeconds);
			pollSeconds = 1;
		}

		await Task.Delay(TimeSpan.FromSeconds(pollSeconds), ct);
	}

	private async Task ProcessJobAsync(ClaimedJobResponse job, CancellationToken ct)
	{
		string? jellyfinTempFile = null;
		try
		{
			string absDiscPath;

			// If this is a Jellyfin-sourced job, download the source file to tmp
			if (!string.IsNullOrEmpty(job.JellyfinDownloadUrl) && !string.IsNullOrEmpty(job.JellyfinApiKey))
			{
				absDiscPath = await DownloadJellyfinSourceAsync(job, ct);
				jellyfinTempFile = absDiscPath;
				_logger.LogInformation("Downloaded Jellyfin source to: {Path}", absDiscPath);
			}
			else
			{
				absDiscPath = ResolveDiscPath(job);
				_logger.LogInformation("Resolved disc path: {Path}", absDiscPath);
			}

			if (!File.Exists(absDiscPath) && !IsDirectoryAccessible(absDiscPath))
			{
				var root = Path.GetPathRoot(absDiscPath) ?? string.Empty;
				var rootExists = !string.IsNullOrWhiteSpace(root) && IsDirectoryAccessible(root);
				var serviceIdentity = $"{Environment.UserDomainName}\\{Environment.UserName}";
				var isDriveLetterPath = absDiscPath.Length >= 2
					&& char.IsLetter(absDiscPath[0])
					&& absDiscPath[1] == ':';

				_logger.LogError(
					"Input path is not accessible. Path={Path}, Root={Root}, RootExists={RootExists}, ServiceIdentity={ServiceIdentity}, ConfiguredRoots={ConfiguredRoots}",
					absDiscPath,
					root,
					rootExists,
					serviceIdentity,
					string.Join(", ", _fsOptions.Roots.Select(r => $"{r.Label}={r.Path}")));

				var hint = isDriveLetterPath
					? " The path uses a mapped drive letter. Windows services often cannot access user-mapped drives; use a UNC path in Filesystem:Roots or run the service under an account that has access."
					: string.Empty;

				var error = $"Input path is not accessible: '{absDiscPath}'.{hint}";
				await _api.UpdateJobStatusAsync(job.JobId, "Failed", error: error, ct: ct);
				return;
			}

			var tracks = JsonSerializer.Deserialize<List<EncodeTrackConfig>>(job.TracksJson) ?? [];
			if (tracks.Count == 0)
			{
				_logger.LogWarning("Job {JobId} has no tracks to encode.", job.JobId);
				await _api.UpdateJobStatusAsync(job.JobId, "Finished", 100, ct: ct);
				return;
			}

			var hasFileTracks = tracks.Any(track => !string.IsNullOrWhiteSpace(track.SourceRelativePath));
			HandBrakeScanResult? discScanResult = null;
			if (!hasFileTracks)
			{
				// Scan disc once and reuse for all tracks.
				discScanResult = await _handBrake.ScanAsync(absDiscPath, ct: ct);
				if (!discScanResult.IsSuccess)
				{
					_logger.LogError("Scan failed for {Path}: {Error}", absDiscPath, discScanResult.Error?.Message);
					await _api.UpdateJobStatusAsync(job.JobId, "Failed", error: $"Scan failed: {discScanResult.Error?.Message}", ct: ct);
					return;
				}
			}

			var rootLookup = _fsOptions.Roots.ToDictionary(r => r.Label, r => r.Path, StringComparer.OrdinalIgnoreCase);

			for (var i = 0; i < tracks.Count; i++)
			{
				var track = tracks[i];
				if (string.IsNullOrWhiteSpace(track.OutputName))
				{
					_logger.LogDebug("Skipping track {Title} (no output name).", track.TitleNumber);
					continue;
				}

				var inputPath = ResolveTrackInputPath(absDiscPath, track);
				var scanResult = discScanResult;
				if (!string.IsNullOrWhiteSpace(track.SourceRelativePath))
				{
					if (!File.Exists(inputPath))
					{
						var error = $"Input file is not accessible: '{inputPath}'.";
						_logger.LogError("{Error}", error);
						await _api.UpdateJobStatusAsync(job.JobId, "Failed", error: error, ct: ct);
						return;
					}

					scanResult = await _handBrake.ScanAsync(inputPath, ct: ct);
					if (!scanResult.IsSuccess)
					{
						var error = $"Scan failed for file '{track.SourceRelativePath}': {scanResult.Error?.Message}";
						_logger.LogError("{Error}", error);
						await _api.UpdateJobStatusAsync(job.JobId, "Failed", error: error, ct: ct);
						return;
					}
				}

				var titleInfo = ResolveTitleInfo(scanResult!, track);
				if (titleInfo is null)
				{
					var error = string.IsNullOrWhiteSpace(track.SourceRelativePath)
						? $"Title {track.TitleNumber} not found in scan results."
						: $"Title {track.TitleNumber} not found in scan results for '{track.SourceRelativePath}'.";
					_logger.LogError("{Error}", error);
					await _api.UpdateJobStatusAsync(job.JobId, "Failed", error: error, ct: ct);
					return;
				}

				// Select audio and subtitle tracks.
				var selectedAudio = AudioTrackSelector.SelectTracks(titleInfo.AudioTracks, job.AudioLanguages, job.AudioDefault);
				var selectedSubtitles = SubtitleTrackSelector.SelectTracks(titleInfo.Subtitles, job.SubtitleLanguages);

				// Build additional CLI args.
				var additionalArgs = HandBrakeArgBuilder.CombineArgs(
					HandBrakeArgBuilder.BuildTitleArg(track.TitleNumber),
					HandBrakeArgBuilder.BuildChapterArgs(track.StartChapter, track.EndChapter),
					HandBrakeArgBuilder.BuildAudioArgs(selectedAudio),
					HandBrakeArgBuilder.BuildSubtitleArgs(selectedSubtitles));

				// Resolve preset path and name.
				string? presetFilePath = null;
				string? presetName = null;
				if (!string.IsNullOrWhiteSpace(track.PresetLabel))
				{
					presetFilePath = ResolvePresetPath(job, track, rootLookup);
					if (job.PresetMap.TryGetValue(track.PresetLabel, out var preset))
					{
						presetName = preset.PresetName;
					}
				}

				// Resolve output path.
				var outputPath = ResolveOutputPath(job, track, rootLookup);

				_logger.LogInformation(
					"Encoding {Source} title {Title} -> {Output} (preset: {Preset})",
					string.IsNullOrWhiteSpace(track.SourceRelativePath) ? absDiscPath : inputPath,
					track.TitleNumber,
					outputPath,
					!string.IsNullOrWhiteSpace(presetName) ? $"{presetName} ({presetFilePath})" : "(no preset)");

				var handBrakeJob = new HandBrakeJob
				{
					InputPath = inputPath,
					OutputPath = outputPath,
					PresetFilePath = presetFilePath,
					PresetName = presetName,
					AdditionalArgs = additionalArgs
				};

				// Build sanitized command for logging (replace absolute root paths with labels).
				var sanitizedCommand = BuildSanitizedCommand(handBrakeJob, rootLookup);

				// Send the command with the first progress update for this track.
				await _api.UpdateJobStatusAsync(job.JobId, "Encoding", 0, 0, encodingCommand: sanitizedCommand, ct: ct);

				// Progress callback: scale per-track progress across the whole job.
				var trackIndex = i;
				var trackCount = tracks.Count;
				var progress = new Progress<ProgressInfo>(p =>
				{
					var overallPercent = (int)(((trackIndex + p.Percent / 100.0) / trackCount) * 100);
					var currentTrackPercent = (int)Math.Clamp(Math.Round(p.Percent), 0, 100);
					_ = _api.UpdateJobStatusAsync(job.JobId, "Encoding", overallPercent, currentTrackPercent, ct: ct);
				});

				using var encodeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
				var cancelMonitorTask = MonitorCancelRequestAsync(job.JobId, encodeCts, ct);

				try
				{
					var encodeResult = await _handBrake.EncodeAsync(handBrakeJob, progress, encodeCts.Token);
					if (!encodeResult.IsSuccess)
					{
						if (await _api.IsCancelRequestedAsync(_runnerOptions.Name, job.JobId, ct))
						{
							await _api.UpdateJobStatusAsync(job.JobId, "Canceled", error: "Encoding canceled by request.", ct: ct);
							_logger.LogInformation("Job {JobId} canceled during encode.", job.JobId);
							return;
						}

						var error = $"Encode failed for title {track.TitleNumber}: {encodeResult.Error?.Message}";
						_logger.LogError("{Error}", error);
						await _api.UpdateJobStatusAsync(job.JobId, "Failed", error: error, ct: ct);
						return;
					}

					_logger.LogInformation(
						"Title {Title} encoded in {Elapsed} (avg {Fps:F1} fps).",
						track.TitleNumber, encodeResult.ElapsedTime, encodeResult.AverageFps);
				}
				catch (OperationCanceledException) when (!ct.IsCancellationRequested)
				{
					await _api.UpdateJobStatusAsync(job.JobId, "Canceled", error: "Encoding canceled by request.", ct: ct);
					_logger.LogInformation("Job {JobId} canceled during encode.", job.JobId);
					return;
				}
				finally
				{
					encodeCts.Cancel();
					await WaitForCancelMonitorAsync(cancelMonitorTask);
				}
			}

			await _api.UpdateJobStatusAsync(job.JobId, "Finished", 100, ct: ct);
			_logger.LogInformation("Job {JobId} completed successfully.", job.JobId);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Unhandled error processing job {JobId}.", job.JobId);
			await _api.UpdateJobStatusAsync(job.JobId, "Failed", error: ex.Message, ct: ct);
		}
		finally
		{
			CleanupTempFile(jellyfinTempFile);
		}
	}

	private async Task MonitorCancelRequestAsync(Guid jobId, CancellationTokenSource encodeCts, CancellationToken ct)
	{
		while (!encodeCts.IsCancellationRequested && !ct.IsCancellationRequested)
		{
			try
			{
				var cancelRequested = await _api.IsCancelRequestedAsync(_runnerOptions.Name, jobId, ct);
				if (cancelRequested)
				{
					encodeCts.Cancel();
					return;
				}

				await Task.Delay(CancelPollInterval, ct);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested || encodeCts.IsCancellationRequested)
			{
				return;
			}
			catch (Exception ex)
			{
				_logger.LogDebug(ex, "Failed to poll cancel state for job {JobId}; will retry.", jobId);
				await Task.Delay(CancelPollInterval, ct);
			}
		}
	}

	private static async Task WaitForCancelMonitorAsync(Task cancelMonitorTask)
	{
		try
		{
			await cancelMonitorTask;
		}
		catch
		{
			// Ignore monitor shutdown errors.
		}
	}

	private string ResolveTrackInputPath(string absDiscPath, EncodeTrackConfig track)
	{
		if (string.IsNullOrWhiteSpace(track.SourceRelativePath))
		{
			return absDiscPath;
		}

		var fullPath = Path.GetFullPath(
			Path.Combine(absDiscPath, _pathNormalizer.ToNative(track.SourceRelativePath)));

		var relative = Path.GetRelativePath(absDiscPath, fullPath);
		if (Path.IsPathRooted(relative)
			|| relative.Equals("..", StringComparison.Ordinal)
			|| relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
			|| relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Track source path '{track.SourceRelativePath}' escapes the selected folder.");
		}

		return fullPath;
	}

	private static TitleInfo? ResolveTitleInfo(HandBrakeScanResult scanResult, EncodeTrackConfig track)
	{
		var titleInfo = scanResult.Titles.FirstOrDefault(t => t.TitleNumber == track.TitleNumber);
		if (titleInfo is not null)
		{
			return titleInfo;
		}

		if (!string.IsNullOrWhiteSpace(track.SourceRelativePath) && scanResult.Titles.Count == 1)
		{
			return scanResult.Titles[0];
		}

		return null;
	}

	private string ResolveDiscPath(ClaimedJobResponse job)
	{
		if (!string.IsNullOrWhiteSpace(job.DiscRootLabel))
		{
			var root = _fsOptions.Roots.FirstOrDefault(
				r => r.Label.Equals(job.DiscRootLabel, StringComparison.OrdinalIgnoreCase));
			if (root is null)
			{
				throw new InvalidOperationException(
					$"Filesystem root '{job.DiscRootLabel}' not configured on this runner.");
			}

			return _pathNormalizer.CombineWithRoot(root.Path, job.DiscPath);
		}

		// No root label — the server stored an absolute path (e.g. Windows drive "G:/Disks/...").
		// Try to match the leading label portion against configured roots so cross-OS runners work.
		// Pattern: "<label>:/" or "<label>:\" at the start of the path.
		var discPath = job.DiscPath;
		foreach (var root in _fsOptions.Roots)
		{
			var prefix = root.Label.TrimEnd('/', '\\') + ":/";
			if (discPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
			{
				var relative = discPath.Substring(prefix.Length).TrimStart('/', '\\');
				return _pathNormalizer.CombineWithRoot(root.Path, relative);
			}

			var prefixBackslash = root.Label.TrimEnd('/', '\\') + ":\\";
			if (discPath.StartsWith(prefixBackslash, StringComparison.OrdinalIgnoreCase))
			{
				var relative = discPath.Substring(prefixBackslash.Length).TrimStart('/', '\\');
				return _pathNormalizer.CombineWithRoot(root.Path, relative);
			}
		}

		return _pathNormalizer.ToNative(discPath);
	}

	private string ResolvePresetPath(
		ClaimedJobResponse job,
		EncodeTrackConfig track,
		Dictionary<string, string> rootLookup)
	{
		var presetLabel = track.PresetLabel ?? string.Empty;
		if (!job.PresetMap.TryGetValue(presetLabel, out var preset))
		{
			throw new InvalidOperationException(
				$"Preset '{presetLabel}' not found in job's preset map.");
		}

		if (!rootLookup.TryGetValue(preset.RootLabel, out var rootPath))
		{
			throw new InvalidOperationException(
				$"Filesystem root '{preset.RootLabel}' not configured on this runner.");
		}

		return _pathNormalizer.CombineWithRoot(rootPath, preset.RelativePath);
	}

	private string ResolveOutputPath(
		ClaimedJobResponse job,
		EncodeTrackConfig track,
		Dictionary<string, string> rootLookup)
	{
		var sourceDisk = Path.GetFileName(
			job.DiscPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/'))
			?? job.DiscPath;

		var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["{TVDBShow}"] = job.TvdbShowName ?? "Unknown",
			["{ShowName}"] = job.TvdbShowName ?? "Unknown",
			["{MovieName}"] = track.OutputName ?? "Unknown",
			["{TVDBID}"] = job.TvdbId?.ToString() ?? string.Empty,
			["{SourceDisk}"] = sourceDisk,
			["{EpisodeName}"] = BuildEpisodeName(track),
			["{OutputName}"] = track.OutputName ?? string.Empty,
			["{SeasonNumber}"] = (track.SeasonNumber ?? 0).ToString("D2"),
			["{EpisodeNumber}"] = (track.EpisodeNumber ?? 0).ToString("D2"),
			["{TitleNumber}"] = track.TitleNumber.ToString("D2"),
			["{MovieYear}"] = ResolveMovieYear(track),
			["{Resolution}"] = track.Resolution ?? string.Empty
		};

		var destRoot = string.Empty;
		if (!string.IsNullOrWhiteSpace(job.TrackDestinationRoot)
			&& rootLookup.TryGetValue(job.TrackDestinationRoot, out var root))
		{
			destRoot = root;
		}

		var expanded = _pathNormalizer.ExpandTemplate(job.TrackDestinationTemplate, variables, destRoot);

		if (string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(expanded)))
		{
			var fallbackName = string.IsNullOrWhiteSpace(track.OutputName)
				? $"Title {track.TitleNumber:D2}"
				: _pathNormalizer.SanitizeSegment(track.OutputName);
			expanded = Path.Combine(expanded.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), fallbackName);
		}

		// Ensure the output path always ends with .mkv
		if (!expanded.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
		{
			expanded += ".mkv";
		}

		return expanded;
	}

	private static string ResolveMovieYear(EncodeTrackConfig track)
	{
		if (!string.IsNullOrWhiteSpace(track.MovieYear))
		{
			return track.MovieYear.Trim();
		}

		var candidates = new[] { track.OutputName, track.SourceRelativePath };
		foreach (var candidate in candidates)
		{
			if (string.IsNullOrWhiteSpace(candidate))
			{
				continue;
			}

			var match = Regex.Match(candidate, @"(?<!\d)(19\d{2}|20\d{2})(?!\d)");
			if (match.Success)
			{
				return match.Value;
			}
		}

		return "Unknown";
	}

	private static string BuildEpisodeName(EncodeTrackConfig track)
	{
		var hasSeason = track.SeasonNumber.HasValue;
		var hasEpisode = track.EpisodeNumber.HasValue && track.EpisodeNumber.Value > 0;
		var hasName = !string.IsNullOrWhiteSpace(track.OutputName);

		if (hasSeason && hasEpisode)
		{
			var prefix = $"s{track.SeasonNumber!.Value:D2}e{track.EpisodeNumber!.Value:D2}";
			return hasName ? $"{prefix} - {track.OutputName}" : prefix;
		}

		if (hasName)
		{
			return track.OutputName;
		}

		if (track.StartChapter.HasValue && track.EndChapter.HasValue)
		{
			return $"Title {track.TitleNumber} Ch {track.StartChapter}-{track.EndChapter}";
		}

		return $"Title {track.TitleNumber}";
	}

	private static string BuildSanitizedCommand(HandBrakeJob job, Dictionary<string, string> rootLookup)
	{
		var command = $"HandBrakeCLI --input {job.InputPath} --output {job.OutputPath}";
		if (!string.IsNullOrEmpty(job.PresetFilePath))
		{
			command += $" --preset-import-file {job.PresetFilePath}";
		}

		if (!string.IsNullOrEmpty(job.AdditionalArgs))
		{
			command += $" {job.AdditionalArgs}";
		}

		// Replace absolute root paths with [label] to avoid exposing full filesystem paths.
		// Process longer paths first so a root like "/mnt/data" is replaced before "/mnt".
		foreach (var root in rootLookup.OrderByDescending(r => r.Value.Length))
		{
			var nativePath = root.Value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			if (!string.IsNullOrEmpty(nativePath))
			{
				command = command.Replace(nativePath, $"[{root.Key}]", StringComparison.OrdinalIgnoreCase);
			}
		}

		return command;
	}

	/// <summary>
	/// Returns <see langword="true"/> if <paramref name="path"/> exists and is accessible.
	/// Unlike <see cref="Directory.Exists"/>, this method also returns <see langword="true"/>
	/// for UNC share roots (e.g. <c>\\server\share</c>) and mounted network drives on Windows
	/// where <see cref="Directory.Exists"/> may return <see langword="false"/> even though the
	/// path is fully accessible.
	/// </summary>
	private static bool IsDirectoryAccessible(string path)
	{
		if (Directory.Exists(path))
		{
			return true;
		}

		// Directory.Exists can return false for UNC share roots and mounted network
		// drives on Windows even when the path is accessible. Probe the directory
		// directly to cover those cases. An empty directory is still accessible.
		try
		{
			using var enumerator = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
			enumerator.MoveNext(); // Opens the directory handle; throws if inaccessible.
			return true;
		}
		catch
		{
			return false;
		}
	}

	private async Task<string> DownloadJellyfinSourceAsync(ClaimedJobResponse job, CancellationToken ct)
	{
		var tempDir = Path.Combine(Path.GetTempPath(), "encoding-manager", job.JobId.ToString());
		Directory.CreateDirectory(tempDir);
		var tempFile = Path.Combine(tempDir, "source.mkv");

		_logger.LogInformation("Downloading Jellyfin source for job {JobId} from {Url}", job.JobId, job.JellyfinDownloadUrl);

		using var client = _httpClientFactory.CreateClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, job.JellyfinDownloadUrl);
		request.Headers.Add("X-Emby-Token", job.JellyfinApiKey);

		using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
		response.EnsureSuccessStatusCode();

		await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
		await response.Content.CopyToAsync(fileStream, ct);

		_logger.LogInformation("Jellyfin source downloaded ({Size} bytes): {Path}", new FileInfo(tempFile).Length, tempFile);
		return tempFile;
	}

	private void CleanupTempFile(string? tempFile)
	{
		if (string.IsNullOrEmpty(tempFile))
		{
			return;
		}

		try
		{
			if (File.Exists(tempFile))
			{
				File.Delete(tempFile);
			}

			var dir = Path.GetDirectoryName(tempFile);
			if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
			{
				Directory.Delete(dir, recursive: true);
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to clean up temp file: {Path}", tempFile);
		}
	}
}
