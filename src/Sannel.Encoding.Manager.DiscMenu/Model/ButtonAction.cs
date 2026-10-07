namespace Sannel.Encoding.Manager.DiscMenu.Model;

/// <summary>The resolved effect of pressing a menu button.</summary>
public class ButtonAction
{
	public ButtonActionType Type { get; set; }

	/// <summary>Target menu for <see cref="ButtonActionType.OpenMenu"/>.</summary>
	public string? MenuId { get; set; }

	/// <summary>HandBrake title number (matches scan_disc). Filled in by the web app.</summary>
	public int? HandBrakeTitle { get; set; }

	/// <summary>DVD global title number (VTS_TT).</summary>
	public int? DvdTitle { get; set; }

	/// <summary>Blu-ray playlist (NNNNN.mpls).</summary>
	public int? Playlist { get; set; }

	/// <summary>First chapter played.</summary>
	public int? StartChapter { get; set; }

	/// <summary>Last chapter played before playback leaves the title. Null = unknown / plays to the end.</summary>
	public int? EndChapter { get; set; }

	/// <summary>Number of chapters in the title.</summary>
	public int? TitleChapterCount { get; set; }

	/// <summary>True when the button plays the entire title (use mode "Titles").</summary>
	public bool? CoversWholeTitle { get; set; }

	/// <summary>What happens after playback: "ReturnToMenu", "PlayTitle", "Stop", "Loop" or "Unknown".</summary>
	public string? Then { get; set; }

	/// <summary>Menu returned to when <see cref="Then"/> is "ReturnToMenu".</summary>
	public string? ThenMenuId { get; set; }

	/// <summary>Title played next when <see cref="Then"/> is "PlayTitle" (DVD title or Blu-ray playlist).</summary>
	public int? ThenTitle { get; set; }

	/// <summary>"Audio", "Subtitle" or "Angle" for <see cref="ButtonActionType.ChangeSetting"/>.</summary>
	public string? Setting { get; set; }

	/// <summary>New stream number for <see cref="ButtonActionType.ChangeSetting"/>.</summary>
	public int? Value { get; set; }

	public ActionConfidence? Confidence { get; set; }

	/// <summary>Why the action is <see cref="ButtonActionType.Unknown"/>, or extra detail.</summary>
	public string? Reason { get; set; }
}
