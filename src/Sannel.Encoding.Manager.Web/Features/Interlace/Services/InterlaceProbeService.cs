using System.Globalization;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Manager.Web.Features.Interlace.Dto;
using Sannel.Encoding.Manager.Web.Features.Interlace.Options;

namespace Sannel.Encoding.Manager.Web.Features.Interlace.Services;

/// <inheritdoc />
public sealed class InterlaceProbeService : IInterlaceProbeService
{
	private readonly IProcessRunner _runner;
	private readonly IFfmpegLocator _locator;
	private readonly InterlaceClassifier _classifier;
	private readonly InterlaceOptions _options;
	private readonly ILogger<InterlaceProbeService> _logger;

	public InterlaceProbeService(
		IProcessRunner runner,
		IFfmpegLocator locator,
		InterlaceClassifier classifier,
		IOptions<InterlaceOptions> options,
		ILogger<InterlaceProbeService> logger)
	{
		this._runner = runner;
		this._locator = locator;
		this._classifier = classifier;
		this._options = options.Value;
		this._logger = logger;
	}

	/// <inheritdoc />
	public async Task<InterlaceMeasurement?> ProbeBlurayTitleAsync(string discPath, int playlist, TimeSpan duration, CancellationToken ct = default)
	{
		var ffmpeg = await this._locator.GetAsync(ct);
		if (!ffmpeg.SupportsBluray)
		{
			return null;
		}

		var input = "bluray:" + discPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		return await this.SampleAsync(ffmpeg.FileName, input, ["-playlist", playlist.ToString(CultureInfo.InvariantCulture)], duration, $"{discPath} playlist {playlist}", ct);
	}

	/// <inheritdoc />
	public async Task<InterlaceMeasurement?> ProbeFileAsync(string filePath, CancellationToken ct = default)
	{
		var ffmpeg = await this._locator.GetAsync(ct);
		if (!ffmpeg.IsAvailable)
		{
			return null;
		}

		// ffmpeg prints the input's duration while failing for lack of an output: no ffprobe needed.
		var header = await this.RunAsync(ffmpeg.FileName, ["-hide_banner", "-nostdin", "-i", filePath], ct);
		if (header is null || IdetParser.ParseDuration(header) is not { } duration)
		{
			this._logger.LogWarning("Interlace probe: could not read the duration of {File}", filePath);
			return null;
		}

		return await this.SampleAsync(ffmpeg.FileName, filePath, [], duration, filePath, ct);
	}

	private async Task<InterlaceMeasurement?> SampleAsync(string ffmpeg, string input, string[] inputOptions, TimeSpan duration, string label, CancellationToken ct)
	{
		var total = IdetCounts.Empty;
		var frames = Math.Max(50, this._options.FramesPerSample).ToString(CultureInfo.InvariantCulture);
		foreach (var point in SamplePoints(this._options.SamplePoints, duration))
		{
			string[] args =
			[
				"-hide_banner", "-nostdin",
				.. inputOptions,
				"-ss", point.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture),
				"-i", input,
				"-map", "0:v:0", "-vf", "idet", "-frames:v", frames, "-an", "-sn", "-dn",
				"-f", "null", "-",
			];
			var stderr = await this.RunAsync(ffmpeg, args, ct);
			if (stderr is null)
			{
				return null;
			}

			total = total.Add(IdetParser.Parse(stderr));
		}

		var measurement = this._classifier.Measure(total);
		this._logger.LogInformation(
			"Interlace probe {Label}: {Verdict} (interlaced {Interlaced:0.#}%, telecine {Telecine:0.#}%, {Frames} frames)",
			label, measurement.Verdict, measurement.InterlacedPercent, measurement.TelecinePercent, measurement.SampledFrames);
		return measurement;
	}

	/// <summary>Seek points: the configured fractions of the duration, clamped so a sample fits; short sources start at 0.</summary>
	public static IEnumerable<TimeSpan> SamplePoints(IEnumerable<double> fractions, TimeSpan duration)
	{
		var seconds = duration.TotalSeconds;
		return fractions
			.Select(f => seconds <= 30 ? 0 : Math.Clamp(f, 0, 0.95) * seconds)
			.Distinct()
			.Select(TimeSpan.FromSeconds);
	}

	/// <summary>Runs ffmpeg and returns its stderr (where idet and the input header are printed); null on timeout or failure to start.</summary>
	private async Task<string?> RunAsync(string ffmpeg, IReadOnlyList<string> args, CancellationToken ct)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, this._options.ProbeTimeoutSeconds)));
		try
		{
			var result = await this._runner.RunAsync(ffmpeg, args, timeout.Token);
			return result.StandardError;
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			this._logger.LogWarning("Interlace probe timed out after {Seconds}s: {Args}", this._options.ProbeTimeoutSeconds, string.Join(' ', args));
			return null;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			this._logger.LogWarning(ex, "Interlace probe failed to run ffmpeg");
			return null;
		}
	}
}
