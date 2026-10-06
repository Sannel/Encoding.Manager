using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.Web.Features.Interlace.Dto;
using Sannel.Encoding.Manager.Web.Features.Interlace.Options;

namespace Sannel.Encoding.Manager.Web.Features.Interlace.Services;

/// <summary>Turns idet counts and HandBrake's comb flag into a verdict, and a verdict into a recommended preset.</summary>
public class InterlaceClassifier
{
	private readonly InterlaceOptions _options;
	private readonly PresetDefaultsOptions _presets;

	public InterlaceClassifier(IOptions<InterlaceOptions> options, IOptions<PresetDefaultsOptions> presets)
	{
		this._options = options.Value;
		this._presets = presets.Value;
	}

	/// <summary>Verdict from ffmpeg idet counts alone (thresholds from <see cref="InterlaceOptions"/>).</summary>
	public InterlaceMeasurement Measure(IdetCounts counts)
	{
		if (counts.Decided < this._options.MinimumDecidedFrames)
		{
			return new InterlaceMeasurement(InterlaceVerdict.Unknown, counts.InterlacedPercent, counts.TelecinePercent, counts.Decided);
		}

		var interlaced = counts.InterlacedPercent >= this._options.InterlacedThresholdPercent;
		var telecined = counts.TelecinePercent >= this._options.TelecineThresholdPercent;
		var verdict = (interlaced, telecined) switch
		{
			(true, true) => InterlaceVerdict.Mixed,
			(true, false) => InterlaceVerdict.Interlaced,
			(false, true) => InterlaceVerdict.Telecined,
			_ => InterlaceVerdict.Progressive,
		};
		return new InterlaceMeasurement(verdict, Math.Round(counts.InterlacedPercent, 1), Math.Round(counts.TelecinePercent, 1), counts.Decided);
	}

	/// <summary>A DVD title: always interlaced (the user decombs every DVD); never probed.</summary>
	public InterlaceResult ForDvd() => this.Result(InterlaceVerdict.Interlaced, "dvd", null);

	/// <summary>A probe is queued or running.</summary>
	public InterlaceResult Pending() => new() { Verdict = InterlaceVerdict.Pending };

	/// <summary>
	/// A Blu-ray title. With a measurement: the idet verdict, except that "progressive" becomes "mixed" when HandBrake saw
	/// combing (decomb is the safe choice). Without one (ffmpeg unavailable or the probe failed): HandBrake's flag only.
	/// </summary>
	public InterlaceResult ForBlurayTitle(InterlaceMeasurement? measurement, bool handBrakeDetected)
	{
		if (measurement is null or { Verdict: InterlaceVerdict.Unknown })
		{
			return this.Result(handBrakeDetected ? InterlaceVerdict.Interlaced : InterlaceVerdict.Progressive, "handbrake", measurement);
		}

		var verdict = measurement.Verdict == InterlaceVerdict.Progressive && handBrakeDetected
			? InterlaceVerdict.Mixed
			: measurement.Verdict;
		return this.Result(verdict, "handbrake+ffmpeg", measurement);
	}

	/// <summary>A media file: the idet verdict, or unknown when there is no measurement.</summary>
	public InterlaceResult ForFile(InterlaceMeasurement? measurement) =>
		measurement is null
			? new InterlaceResult { Verdict = InterlaceVerdict.Unknown, Source = "ffmpeg" }
			: this.Result(measurement.Verdict, "ffmpeg", measurement);

	/// <summary>The preset a verdict suggests, or null for pending / unknown.</summary>
	public string? RecommendedPreset(InterlaceVerdict verdict) => verdict switch
	{
		InterlaceVerdict.Interlaced or InterlaceVerdict.Telecined or InterlaceVerdict.Mixed => this._presets.InterlacedPresetLabel,
		InterlaceVerdict.Progressive => this._presets.ProgressivePresetLabel,
		_ => null,
	};

	private InterlaceResult Result(InterlaceVerdict verdict, string source, InterlaceMeasurement? measurement) => new()
	{
		Verdict = verdict,
		Source = source,
		InterlacedPercent = measurement?.InterlacedPercent,
		TelecinePercent = measurement?.TelecinePercent,
		RecommendedPreset = this.RecommendedPreset(verdict),
	};
}
