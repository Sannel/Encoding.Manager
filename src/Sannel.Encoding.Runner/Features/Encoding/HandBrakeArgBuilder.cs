namespace Sannel.Encoding.Runner.Features.Encoding;

/// <summary>Builds HandBrake CLI arguments for audio/subtitle track selection and title/chapter ranges.</summary>
public static class HandBrakeArgBuilder
{
	/// <summary>
	/// Builds the audio-related CLI arguments:
	/// --audio 1,1,2 --aencoder opus,copy,copy --mixdown stereo,auto,auto [--arate 48,Auto,Auto] [--ab 640,0,0]
	/// </summary>
	public static string BuildAudioArgs(IReadOnlyList<SelectedAudioTrack> tracks)
	{
		if (tracks.Count == 0)
		{
			return string.Empty;
		}

		var numbers = string.Join(",", tracks.Select(t => t.TrackNumber));
		var encoders = string.Join(",", tracks.Select(t => t.Encoder));
		var mixdowns = string.Join(",", tracks.Select(t => t.Mixdown));

		var args = $"--audio {numbers} --aencoder {encoders} --mixdown {mixdowns}";

		if (tracks.Any(t => t.SampleRate > 0))
		{
			var rates = string.Join(",", tracks.Select(t => FormatSampleRateKhz(t.SampleRate)));
			args += $" --arate {rates}";
		}

		if (tracks.Any(t => t.Bitrate > 0))
		{
			var bitrates = string.Join(",", tracks.Select(t => t.Bitrate > 0 ? (t.Bitrate / 1000).ToString() : "0"));
			args += $" --ab {bitrates}";
		}

		return args;
	}

	/// <summary>Converts a sample rate in Hz to the kHz string HandBrake CLI expects (e.g. 48000 → "48", 44100 → "44.1").</summary>
	private static string FormatSampleRateKhz(int hz) =>
		hz > 0 ? (hz / 1000.0).ToString("0.##") : "Auto";

	/// <summary>
	/// Builds the subtitle-related CLI arguments:
	/// --subtitle 1,2
	/// </summary>
	public static string BuildSubtitleArgs(IReadOnlyList<int> trackNumbers)
	{
		if (trackNumbers.Count == 0)
		{
			return string.Empty;
		}

		return $"--subtitle {string.Join(",", trackNumbers)}";
	}

	/// <summary>
	/// Builds the title selection argument: --title 3
	/// </summary>
	public static string BuildTitleArg(int titleNumber) =>
		$"--title {titleNumber}";

	/// <summary>
	/// Builds chapter range arguments: --chapters 1-5
	/// Returns empty string if neither start nor end chapter is specified.
	/// </summary>
	public static string BuildChapterArgs(int? startChapter, int? endChapter)
	{
		if (startChapter is null && endChapter is null)
		{
			return string.Empty;
		}

		var start = startChapter ?? 1;
		var end = endChapter ?? start;

		return $"--chapters {start}-{end}";
	}

	/// <summary>
	/// Combines all additional arguments into a single string for HandBrakeJob.AdditionalArgs.
	/// </summary>
	public static string CombineArgs(params string[] args) =>
		string.Join(" ", args.Where(a => !string.IsNullOrWhiteSpace(a)));
}
