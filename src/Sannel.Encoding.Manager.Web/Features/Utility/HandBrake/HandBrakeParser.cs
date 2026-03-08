using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sannel.Encoding.Manager.Web.Features.Utility.HandBrake;

/// <summary>
/// Stateless parser for HandBrakeCLI output.
/// Prefers JSON scan output; falls back to text parsing.
/// </summary>
public static partial class HandBrakeParser
{
	/// <summary>
	/// Attempts to parse the version from HandBrakeCLI --version output.
	/// Expected format: "HandBrake 10.1.0" or similar.
	/// </summary>
	public static bool TryParseVersion(string output, out Version? version)
	{
		version = null;
		if (string.IsNullOrWhiteSpace(output))
		{
			return false;
		}

		var match = VersionRegex().Match(output);
		if (match.Success && Version.TryParse(match.Groups[1].Value, out version))
		{
			return true;
		}

		return false;
	}

	/// <summary>
	/// Parses a scan result from HandBrakeCLI output. Attempts JSON first, then text fallback.
	/// </summary>
	public static IReadOnlyList<TitleInfo> ParseScan(string output)
	{
		if (string.IsNullOrWhiteSpace(output))
		{
			return [];
		}

		// Try JSON parse first
		var jsonStart = output.IndexOf('{');
		if (jsonStart >= 0)
		{
			try
			{
				return ParseScanJson(output[jsonStart..]);
			}
			catch (JsonException)
			{
				// Fall through to text parsing
			}
		}

		return ParseScanText(output);
	}

	/// <summary>
	/// Parses a single progress line from HandBrakeCLI stdout during encoding.
	/// Returns null if the line is not a progress line.
	/// </summary>
	public static ProgressInfo? ParseProgressLine(string line)
	{
		if (string.IsNullOrWhiteSpace(line))
		{
			return null;
		}

		// HandBrakeCLI progress format:
		// Encoding: task 1 of 1, 45.23 % (123.45 fps, avg 100.00 fps, ETA 00h12m34s)
		// Muxing: this may appear as well
		var match = ProgressRegex().Match(line);
		if (!match.Success)
		{
			return null;
		}

		var phase = match.Groups["phase"].Value;
		_ = double.TryParse(match.Groups["percent"].Value, out var percent);
		_ = double.TryParse(match.Groups["fps"].Value, out var currentFps);
		_ = double.TryParse(match.Groups["avgfps"].Value, out var avgFps);

		TimeSpan? eta = null;
		if (match.Groups["etah"].Success
			&& int.TryParse(match.Groups["etah"].Value, out var h)
			&& int.TryParse(match.Groups["etam"].Value, out var m)
			&& int.TryParse(match.Groups["etas"].Value, out var s))
		{
			eta = new TimeSpan(h, m, s);
		}

		return new ProgressInfo
		{
			Percent = percent,
			CurrentPhase = phase,
			CurrentFps = currentFps,
			AverageFps = avgFps,
			Eta = eta
		};
	}

	private static IReadOnlyList<TitleInfo> ParseScanJson(string json)
	{
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		if (!root.TryGetProperty("TitleList", out var titleList))
		{
			return [];
		}

		var titles = new List<TitleInfo>();

		foreach (var title in titleList.EnumerateArray())
		{
			var titleInfo = new TitleInfo
			{
				TitleNumber = title.TryGetProperty("Index", out var idx) ? idx.GetInt32() : 0,
				Duration = ParseDuration(title),
				FrameRate = ParseFrameRate(title),
				Width = title.TryGetProperty("Geometry", out var geo)
					&& geo.TryGetProperty("Width", out var w) ? w.GetInt32() : 0,
				Height = title.TryGetProperty("Geometry", out var geo2)
					&& geo2.TryGetProperty("Height", out var h) ? h.GetInt32() : 0,
				VideoStreams = ParseVideoStreams(title),
				AudioTracks = ParseAudioTracks(title),
				Subtitles = ParseSubtitles(title),
				Chapters = ParseChapters(title)
			};

			titles.Add(titleInfo);
		}

		return titles;
	}

	private static TimeSpan ParseDuration(JsonElement title)
	{
		if (title.TryGetProperty("Duration", out var dur))
		{
			var hours = dur.TryGetProperty("Hours", out var h) ? h.GetInt32() : 0;
			var minutes = dur.TryGetProperty("Minutes", out var m) ? m.GetInt32() : 0;
			var seconds = dur.TryGetProperty("Seconds", out var s) ? s.GetInt32() : 0;
			return new TimeSpan(hours, minutes, seconds);
		}

		return TimeSpan.Zero;
	}

	private static double ParseFrameRate(JsonElement title)
	{
		if (title.TryGetProperty("FrameRate", out var fr))
		{
			if (fr.TryGetProperty("Num", out var num) && fr.TryGetProperty("Den", out var den))
			{
				var denominator = den.GetDouble();
				return denominator > 0 ? num.GetDouble() / denominator : 0;
			}

			if (fr.ValueKind == JsonValueKind.Number)
			{
				return fr.GetDouble();
			}
		}

		return 0;
	}

	private static IReadOnlyList<VideoStreamInfo> ParseVideoStreams(JsonElement title)
	{
		// HandBrake JSON scan doesn't have a separate video streams array;
		// the title itself describes the main video stream.
		var codec = title.TryGetProperty("VideoCodec", out var vc) ? vc.GetString() ?? "" : "";
		var width = title.TryGetProperty("Geometry", out var geo) && geo.TryGetProperty("Width", out var w) ? w.GetInt32() : 0;
		var height = title.TryGetProperty("Geometry", out var geo2) && geo2.TryGetProperty("Height", out var h) ? h.GetInt32() : 0;
		var frameRate = ParseFrameRate(title);

		if (width == 0 && height == 0 && string.IsNullOrEmpty(codec))
		{
			return [];
		}

		return [new VideoStreamInfo { Codec = codec, Width = width, Height = height, FrameRate = frameRate }];
	}

	private static IReadOnlyList<AudioTrackInfo> ParseAudioTracks(JsonElement title)
	{
		if (!title.TryGetProperty("AudioList", out var audioList))
		{
			return [];
		}

		var tracks = new List<AudioTrackInfo>();
		var trackNum = 1;

		foreach (var audio in audioList.EnumerateArray())
		{
			tracks.Add(new AudioTrackInfo
			{
				TrackNumber = trackNum++,
				Codec = audio.TryGetProperty("CodecName", out var c)
					? c.GetString() ?? ""
					: audio.TryGetProperty("Description", out var d) ? d.GetString() ?? "" : "",
				Language = audio.TryGetProperty("LanguageCode", out var lang)
					? lang.GetString() ?? ""
					: audio.TryGetProperty("Language", out var l) ? l.GetString() ?? "" : "",
				SampleRate = audio.TryGetProperty("SampleRate", out var sr) ? sr.GetInt32() : 0,
				Bitrate = audio.TryGetProperty("BitRate", out var br) ? br.GetInt32() : 0,
				ChannelLayout = audio.TryGetProperty("ChannelLayout", out var cl)
					? cl.GetString() ?? ""
					: audio.TryGetProperty("ChannelLayoutName", out var cln) ? cln.GetString() ?? "" : ""
			});
		}

		return tracks;
	}

	private static IReadOnlyList<SubtitleInfo> ParseSubtitles(JsonElement title)
	{
		if (!title.TryGetProperty("SubtitleList", out var subtitleList))
		{
			return [];
		}

		var subs = new List<SubtitleInfo>();
		var trackNum = 1;

		foreach (var sub in subtitleList.EnumerateArray())
		{
			subs.Add(new SubtitleInfo
			{
				TrackNumber = trackNum++,
				Language = sub.TryGetProperty("LanguageCode", out var lang)
					? lang.GetString() ?? ""
					: sub.TryGetProperty("Language", out var l) ? l.GetString() ?? "" : "",
				Format = sub.TryGetProperty("SourceName", out var src)
					? src.GetString() ?? ""
					: sub.TryGetProperty("Format", out var f) ? f.GetString() ?? "" : ""
			});
		}

		return subs;
	}

	private static IReadOnlyList<ChapterInfo> ParseChapters(JsonElement title)
	{
		if (!title.TryGetProperty("ChapterList", out var chapterList))
		{
			return [];
		}

		var chapters = new List<ChapterInfo>();
		var chapterNum = 1;

		foreach (var chapter in chapterList.EnumerateArray())
		{
			var duration = TimeSpan.Zero;
			if (chapter.TryGetProperty("Duration", out var dur))
			{
				var hours = dur.TryGetProperty("Hours", out var h) ? h.GetInt32() : 0;
				var minutes = dur.TryGetProperty("Minutes", out var m) ? m.GetInt32() : 0;
				var seconds = dur.TryGetProperty("Seconds", out var s) ? s.GetInt32() : 0;
				duration = new TimeSpan(hours, minutes, seconds);
			}

			chapters.Add(new ChapterInfo
			{
				ChapterNumber = chapterNum++,
				Name = chapter.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "",
				Duration = duration
			});
		}

		return chapters;
	}

	private static IReadOnlyList<TitleInfo> ParseScanText(string output)
	{
		// Text-based fallback parser for older HandBrakeCLI versions without JSON.
		// Parses "title N:" blocks from scan output.
		var titles = new List<TitleInfo>();
		var titleMatches = TitleBlockRegex().Matches(output);

		for (var i = 0; i < titleMatches.Count; i++)
		{
			var titleMatch = titleMatches[i];
			if (!int.TryParse(titleMatch.Groups[1].Value, out var titleNumber))
			{
				continue;
			}

			// Extract the block text from this title header to the next (or end of string)
			var blockStart = titleMatch.Index;
			var blockEnd = i + 1 < titleMatches.Count
				? titleMatches[i + 1].Index
				: output.Length;
			var titleBlock = output[blockStart..blockEnd];
			var duration = TimeSpan.Zero;
			var durationMatch = TextDurationRegex().Match(titleBlock);
			if (durationMatch.Success
				&& int.TryParse(durationMatch.Groups[1].Value, out var h)
				&& int.TryParse(durationMatch.Groups[2].Value, out var m)
				&& int.TryParse(durationMatch.Groups[3].Value, out var s))
			{
				duration = new TimeSpan(h, m, s);
			}

			var width = 0;
			var height = 0;
			var sizeMatch = TextResolutionRegex().Match(titleBlock);
			if (sizeMatch.Success)
			{
				_ = int.TryParse(sizeMatch.Groups[1].Value, out width);
				_ = int.TryParse(sizeMatch.Groups[2].Value, out height);
			}

			titles.Add(new TitleInfo
			{
				TitleNumber = titleNumber,
				Duration = duration,
				Width = width,
				Height = height
			});
		}

		return titles;
	}

	[GeneratedRegex(@"HandBrake\s+(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
	private static partial Regex VersionRegex();

	[GeneratedRegex(@"(?<phase>Encoding|Muxing):.*?(?<percent>\d+\.\d+)\s*%\s*\((?<fps>\d+\.?\d*)\s*fps,\s*avg\s*(?<avgfps>\d+\.?\d*)\s*fps,\s*ETA\s*(?<etah>\d+)h(?<etam>\d+)m(?<etas>\d+)s\)", RegexOptions.IgnoreCase)]
	private static partial Regex ProgressRegex();

	[GeneratedRegex(@"^\+ title (\d+):", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
	private static partial Regex TitleBlockRegex();

	[GeneratedRegex(@"duration:\s*(\d+):(\d+):(\d+)", RegexOptions.IgnoreCase)]
	private static partial Regex TextDurationRegex();

	[GeneratedRegex(@"(\d+)x(\d+)", RegexOptions.IgnoreCase)]
	private static partial Regex TextResolutionRegex();
}
