namespace Sannel.Encoding.Manager.Web.Features.Interlace.Dto;

/// <summary>The interlace verdict for one title or file, with the preset it suggests.</summary>
public sealed class InterlaceResult
{
	public required InterlaceVerdict Verdict { get; init; }

	/// <summary>Where the verdict came from: "dvd", "handbrake+ffmpeg", "handbrake" or "ffmpeg"; null while pending.</summary>
	public string? Source { get; init; }

	public double? InterlacedPercent { get; init; }

	public double? TelecinePercent { get; init; }

	/// <summary>Preset suggested by the verdict (config <c>Presets</c>); null while pending or unknown. Never applied automatically.</summary>
	public string? RecommendedPreset { get; init; }

	/// <summary>True when the verdict means the source should be decombed.</summary>
	public bool NeedsDecomb => this.Verdict is InterlaceVerdict.Interlaced or InterlaceVerdict.Telecined or InterlaceVerdict.Mixed;
}
