namespace Sannel.Encoding.Manager.Web.Features.Settings.Entities;

/// <summary>
/// Singleton application settings row — always stored with Id = 1.
/// </summary>
public class AppSettings
{
	public int Id { get; set; } = 1;

	/// <summary>
	/// Output file path template. Supported variables:
	/// {SourceDisk}, {TVDBShow}, {TVDBID}, {SeasonNumber}, {EpisodeName}.
	///
	/// {SeasonNumber} expands to "Season 01" / "Season 12".
	/// {EpisodeName}  expands to "s01e05 - Title.mkv"; omits season/episode when
	/// unknown; falls back to "disk Title {N} Chapters.mkv" when no episode title.
	/// </summary>
	public string TrackDestinationTemplate { get; set; } =
		@"{TVDBShow}\Season {SeasonNumber}\{EpisodeName}";

	/// <summary>
	/// The label of the configured filesystem root to use as the base output directory.
	/// Matches a label from FilesystemOptions.Roots.
	/// </summary>
	public string? TrackDestinationRoot { get; set; }

	/// <summary>Default audio codec: "Opus" or "Aac".</summary>
	public string AudioDefault { get; set; } = "Opus";
}
