using Sannel.Encoding.Manager.HandBrake;

namespace Sannel.Encoding.Runner.Features.Encoding;

/// <summary>Selects subtitle tracks from a title based on configured language preferences.</summary>
public static class SubtitleTrackSelector
{
	/// <summary>
	/// Returns the track numbers of all subtitle tracks whose language matches
	/// any of the configured languages.
	/// </summary>
	public static IReadOnlyList<int> SelectTracks(
		IReadOnlyList<SubtitleInfo> subtitles,
		string[] configuredLanguages)
	{
		if (subtitles.Count == 0 || configuredLanguages.Length == 0)
		{
			return [];
		}

		var languageSet = new HashSet<string>(configuredLanguages, StringComparer.OrdinalIgnoreCase);

		return subtitles
			.Where(s => languageSet.Contains(s.Language))
			.Select(s => s.TrackNumber)
			.ToList();
	}
}
