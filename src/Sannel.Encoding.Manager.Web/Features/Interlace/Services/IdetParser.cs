using System.Globalization;
using System.Text.RegularExpressions;
using Sannel.Encoding.Manager.Web.Features.Interlace.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Interlace.Services;

/// <summary>Parses ffmpeg's stderr: <c>idet</c> filter summaries and the input's duration.</summary>
public static partial class IdetParser
{
	/// <summary>
	/// Sums every "Multi frame detection" and "Repeated Fields" summary in the output. ffmpeg can print the summary more
	/// than once per run (an all-zero one from a reinitialised filter graph), so every line is added.
	/// </summary>
	public static IdetCounts Parse(string stderr)
	{
		var counts = IdetCounts.Empty;
		foreach (Match m in MultiFrameRegex().Matches(stderr))
		{
			counts = counts.Add(new IdetCounts(Int(m, "tff"), Int(m, "bff"), Int(m, "prog"), Int(m, "und"), 0, 0, 0));
		}

		foreach (Match m in RepeatedRegex().Matches(stderr))
		{
			counts = counts.Add(new IdetCounts(0, 0, 0, 0, Int(m, "neither"), Int(m, "top"), Int(m, "bottom")));
		}

		return counts;
	}

	/// <summary>The input's duration from ffmpeg's "Duration: hh:mm:ss.ff" header line, or null when not present.</summary>
	public static TimeSpan? ParseDuration(string stderr)
	{
		var m = DurationRegex().Match(stderr);
		if (!m.Success)
		{
			return null;
		}

		return new TimeSpan(0, Int(m, "h"), Int(m, "m"), Int(m, "s"))
			+ TimeSpan.FromSeconds(double.Parse("0." + m.Groups["f"].Value, CultureInfo.InvariantCulture));
	}

	private static int Int(Match m, string group) => int.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture);

	[GeneratedRegex(@"Multi frame detection:\s*TFF:\s*(?<tff>\d+)\s*BFF:\s*(?<bff>\d+)\s*Progressive:\s*(?<prog>\d+)\s*Undetermined:\s*(?<und>\d+)")]
	private static partial Regex MultiFrameRegex();

	[GeneratedRegex(@"Repeated Fields:\s*Neither:\s*(?<neither>\d+)\s*Top:\s*(?<top>\d+)\s*Bottom:\s*(?<bottom>\d+)")]
	private static partial Regex RepeatedRegex();

	[GeneratedRegex(@"Duration:\s*(?<h>\d+):(?<m>\d{2}):(?<s>\d{2})\.(?<f>\d+)")]
	private static partial Regex DurationRegex();
}
