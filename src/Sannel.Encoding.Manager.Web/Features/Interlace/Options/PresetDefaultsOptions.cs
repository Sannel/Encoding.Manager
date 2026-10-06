namespace Sannel.Encoding.Manager.Web.Features.Interlace.Options;

/// <summary>
/// Presets suggested from an interlace verdict (config section <c>Presets</c>). Only ever a recommendation: the server
/// never applies a preset the request did not name.
/// </summary>
public class PresetDefaultsOptions
{
	/// <summary>Suggested for interlaced, telecined and mixed sources (and every DVD title).</summary>
	public string InterlacedPresetLabel { get; set; } = "4K AV1 Decomb";

	/// <summary>Suggested for progressive sources.</summary>
	public string ProgressivePresetLabel { get; set; } = "4K AV1";
}
