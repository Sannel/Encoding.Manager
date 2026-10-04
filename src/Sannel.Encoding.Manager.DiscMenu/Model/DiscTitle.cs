namespace Sannel.Encoding.Manager.DiscMenu.Model;

/// <summary>A title (DVD) or playlist (Blu-ray) on the disc and which buttons play it.</summary>
public class DiscTitle
{
	/// <summary>HandBrake title number. Filled in by the web app.</summary>
	public int? HandBrakeTitle { get; set; }

	public int? DvdTitle { get; set; }

	public int? Playlist { get; set; }

	public int ChapterCount { get; set; }

	public int DurationSeconds { get; set; }

	/// <summary>Buttons that play this title, as "menuId#button".</summary>
	public List<string> ReachedFromButtons { get; set; } = [];
}
