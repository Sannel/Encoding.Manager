using System.Text.Json;
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
	private readonly ILogger<EncodingWorkerService> _logger;

	public EncodingWorkerService(
		IRunnerApiClient api,
		IHandBrakeService handBrake,
		PathNormalizer pathNormalizer,
		IOptions<RunnerOptions> runnerOptions,
		IOptions<FilesystemOptions> fsOptions,
		ILogger<EncodingWorkerService> logger)
	{
		_api = api;
		_handBrake = handBrake;
		_pathNormalizer = pathNormalizer;
		_runnerOptions = runnerOptions.Value;
		_fsOptions = fsOptions.Value;
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
					await Task.Delay(TimeSpan.FromSeconds(_runnerOptions.PollIntervalSeconds), stoppingToken);
					continue;
				}

				var job = await _api.ClaimNextJobAsync(_runnerOptions.Name, stoppingToken);
				if (job is null)
				{
					_logger.LogDebug("No jobs available, sleeping.");
					await Task.Delay(TimeSpan.FromSeconds(_runnerOptions.PollIntervalSeconds), stoppingToken);
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
				await Task.Delay(TimeSpan.FromSeconds(_runnerOptions.PollIntervalSeconds), stoppingToken);
			}
		}

		_logger.LogInformation("Encoding worker '{Name}' stopping.", _runnerOptions.Name);
	}

	private async Task ProcessJobAsync(ClaimedJobResponse job, CancellationToken ct)
	{
		try
		{
			var absDiscPath = ResolveDiscPath(job);
			_logger.LogInformation("Resolved disc path: {Path}", absDiscPath);

			if (!File.Exists(absDiscPath) && !Directory.Exists(absDiscPath))
			{
				var root = Path.GetPathRoot(absDiscPath) ?? string.Empty;
				var rootExists = !string.IsNullOrWhiteSpace(root) && Directory.Exists(root);
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

			// Scan disc once and reuse for all tracks.
			var scanResult = await _handBrake.ScanAsync(absDiscPath, ct: ct);
			if (!scanResult.IsSuccess)
			{
				_logger.LogError("Scan failed for {Path}: {Error}", absDiscPath, scanResult.Error?.Message);
				await _api.UpdateJobStatusAsync(job.JobId, "Failed", error: $"Scan failed: {scanResult.Error?.Message}", ct: ct);
				return;
			}

			var tracks = JsonSerializer.Deserialize<List<EncodeTrackConfig>>(job.TracksJson) ?? [];
			if (tracks.Count == 0)
			{
				_logger.LogWarning("Job {JobId} has no tracks to encode.", job.JobId);
				await _api.UpdateJobStatusAsync(job.JobId, "Finished", 100, ct: ct);
				return;
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

				var titleInfo = scanResult.Titles.FirstOrDefault(t => t.TitleNumber == track.TitleNumber);
				if (titleInfo is null)
				{
					var error = $"Title {track.TitleNumber} not found in scan results.";
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

				// Resolve preset path.
				var presetFilePath = ResolvePresetPath(job, track, rootLookup);

				// Resolve output path.
				var outputPath = ResolveOutputPath(job, track, rootLookup);

				_logger.LogInformation(
					"Encoding title {Title} -> {Output} (preset: {Preset})",
					track.TitleNumber, outputPath, presetFilePath);

				var handBrakeJob = new HandBrakeJob
				{
					InputPath = absDiscPath,
					OutputPath = outputPath,
					PresetFilePath = presetFilePath,
					AdditionalArgs = additionalArgs
				};

				// Progress callback: scale per-track progress across the whole job.
				var trackIndex = i;
				var trackCount = tracks.Count;
				var progress = new Progress<ProgressInfo>(p =>
				{
					var overallPercent = (int)(((trackIndex + p.Percent / 100.0) / trackCount) * 100);
					_ = _api.UpdateJobStatusAsync(job.JobId, "Encoding", overallPercent, ct: ct);
				});

				var encodeResult = await _handBrake.EncodeAsync(handBrakeJob, progress, ct);
				if (!encodeResult.IsSuccess)
				{
					var error = $"Encode failed for title {track.TitleNumber}: {encodeResult.Error?.Message}";
					_logger.LogError("{Error}", error);
					await _api.UpdateJobStatusAsync(job.JobId, "Failed", error: error, ct: ct);
					return;
				}

				_logger.LogInformation(
					"Title {Title} encoded in {Elapsed} (avg {Fps:F1} fps).",
					track.TitleNumber, encodeResult.ElapsedTime, encodeResult.AverageFps);
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

		return _pathNormalizer.ToNative(job.DiscPath);
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
			["{TVDBID}"] = job.TvdbId?.ToString() ?? string.Empty,
			["{SourceDisk}"] = sourceDisk,
			["{EpisodeName}"] = BuildEpisodeName(track),
			["{OutputName}"] = track.OutputName,
			["{SeasonNumber}"] = (track.SeasonNumber ?? 0).ToString("D2"),
			["{EpisodeNumber}"] = (track.EpisodeNumber ?? 0).ToString("D2"),
			["{TitleNumber}"] = track.TitleNumber.ToString("D2")
		};

		var destRoot = string.Empty;
		if (!string.IsNullOrWhiteSpace(job.TrackDestinationRoot)
			&& rootLookup.TryGetValue(job.TrackDestinationRoot, out var root))
		{
			destRoot = root;
		}

		var expanded = _pathNormalizer.ExpandTemplate(job.TrackDestinationTemplate, variables, destRoot);

		// Ensure the output path always ends with .mkv
		if (!expanded.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
		{
			expanded += ".mkv";
		}

		return expanded;
	}

	private static string BuildEpisodeName(EncodeTrackConfig track)
	{
		var hasSeason = track.SeasonNumber.HasValue && track.SeasonNumber.Value > 0;
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
}
