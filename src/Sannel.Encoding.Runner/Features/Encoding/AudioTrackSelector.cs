using Sannel.Encoding.Manager.HandBrake;

namespace Sannel.Encoding.Runner.Features.Encoding;

/// <summary>Represents a selected audio track with its encoding settings.</summary>
/// <param name="SampleRate">Source sample rate in Hz (0 = Auto).</param>
/// <param name="Bitrate">Source bitrate in bps (0 = Auto).</param>
public record SelectedAudioTrack(int TrackNumber, string Encoder, string Mixdown, int SampleRate = 0, int Bitrate = 0);

/// <summary>Selects audio tracks from a title based on configured language preferences.</summary>
public static class AudioTrackSelector
{
	/// <summary>
	/// Selects audio tracks from the given title's audio tracks.
	/// The first track matching any configured language gets a stereo downmix with the specified codec.
	/// All remaining tracks are copied as-is.
	/// </summary>
	public static IReadOnlyList<SelectedAudioTrack> SelectTracks(
		IReadOnlyList<AudioTrackInfo> audioTracks,
		string[] configuredLanguages,
		string audioDefaultCodec)
	{
		if (audioTracks.Count == 0)
		{
			return [];
		}

		var results = new List<SelectedAudioTrack>();
		var languageSet = new HashSet<string>(configuredLanguages, StringComparer.OrdinalIgnoreCase);
		var firstMatchFound = false;

		foreach (var track in audioTracks)
		{
			if (!firstMatchFound && languageSet.Contains(track.Language))
			{
				firstMatchFound = true;
				// Use AudioDefault codec from server settings; preserve source sample rate and bitrate.
				results.Add(new SelectedAudioTrack(
					track.TrackNumber,
					audioDefaultCodec,
					"stereo",
					track.SampleRate,
					track.Bitrate));
			}
			else
			{
				results.Add(new SelectedAudioTrack(track.TrackNumber, "copy", "auto"));
			}
		}

		return results;
	}
}
