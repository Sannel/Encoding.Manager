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
	/// Selects audio tracks whose language matches any of the configured languages.
	/// The first matching track is always preserved as a copy.
	/// If that first matching track is not already AAC stereo, an additional stereo
	/// encode is added using the specified codec.
	/// All remaining matching tracks are copied as-is.
	/// Tracks whose language does not match are excluded entirely.
	/// </summary>
	public static IReadOnlyList<SelectedAudioTrack> SelectTracks(
		IReadOnlyList<AudioTrackInfo> audioTracks,
		string[] configuredLanguages,
		string audioDefaultCodec)
	{
		if (audioTracks.Count == 0 || configuredLanguages.Length == 0)
		{
			return [];
		}

		var results = new List<SelectedAudioTrack>();
		var languageSet = new HashSet<string>(configuredLanguages, StringComparer.OrdinalIgnoreCase);
		var firstMatchFound = false;

		foreach (var track in audioTracks)
		{
			if (!languageSet.Contains(track.Language))
			{
				continue;
			}

			if (!firstMatchFound)
			{
				firstMatchFound = true;
				if (!IsAacStereo(track))
				{
					results.Add(new SelectedAudioTrack(
						track.TrackNumber,
						audioDefaultCodec,
						"stereo",
						track.SampleRate,
						track.Bitrate));
				}

				results.Add(new SelectedAudioTrack(track.TrackNumber, "copy", "auto"));
			}
			else
			{
				results.Add(new SelectedAudioTrack(track.TrackNumber, "copy", "auto"));
			}
		}

		return results;
	}

	private static bool IsAacStereo(AudioTrackInfo track) =>
		track.Codec.Contains("aac", StringComparison.OrdinalIgnoreCase)
		&& track.ChannelLayout.Contains("stereo", StringComparison.OrdinalIgnoreCase);
}
