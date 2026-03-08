using Sannel.Encoding.Manager.Web.Features.Utility.HandBrake;

namespace Sannel.Encoding.Manager.HandBrake.Tests;

public class HandBrakeParserTests
{
	[Theory]
	[InlineData("HandBrake 10.1.0", 10, 1, 0)]
	[InlineData("HandBrake 10.1", 10, 1)]
	[InlineData("HandBrake 1.7.3", 1, 7, 3)]
	[InlineData("HandBrake 10.2.1 (2026030100)", 10, 2, 1)]
	public void TryParseVersion_ValidOutput_ReturnsTrue(
		string output, int major, int minor, int build = -1)
	{
		var result = HandBrakeParser.TryParseVersion(output, out var version);

		Assert.True(result);
		Assert.NotNull(version);
		Assert.Equal(major, version!.Major);
		Assert.Equal(minor, version.Minor);
		if (build >= 0)
		{
			Assert.Equal(build, version.Build);
		}
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("some random text")]
	[InlineData("HandBrake")]
	public void TryParseVersion_InvalidOutput_ReturnsFalse(string output)
	{
		var result = HandBrakeParser.TryParseVersion(output, out var version);

		Assert.False(result);
		Assert.Null(version);
	}

	[Fact]
	public void ParseProgressLine_ValidEncodingLine_ReturnsParsedInfo()
	{
		var line = "Encoding: task 1 of 1, 45.23 % (123.45 fps, avg 100.00 fps, ETA 00h12m34s)";

		var progress = HandBrakeParser.ParseProgressLine(line);

		Assert.NotNull(progress);
		Assert.Equal(45.23, progress!.Percent, 0.01);
		Assert.Equal("Encoding", progress.CurrentPhase);
		Assert.Equal(123.45, progress.CurrentFps, 0.01);
		Assert.Equal(100.00, progress.AverageFps, 0.01);
		Assert.Equal(new TimeSpan(0, 12, 34), progress.Eta);
	}

	[Fact]
	public void ParseProgressLine_MuxingLine_ReturnsParsedInfo()
	{
		var line = "Muxing: task 1 of 1, 99.50 % (200.00 fps, avg 150.00 fps, ETA 00h00m05s)";

		var progress = HandBrakeParser.ParseProgressLine(line);

		Assert.NotNull(progress);
		Assert.Equal(99.50, progress!.Percent, 0.01);
		Assert.Equal("Muxing", progress.CurrentPhase);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("some random log line")]
	[InlineData("Scanning title 1 of 3...")]
	public void ParseProgressLine_NonProgressLine_ReturnsNull(string line)
	{
		var progress = HandBrakeParser.ParseProgressLine(line);

		Assert.Null(progress);
	}

	[Fact]
	public void ParseScan_EmptyInput_ReturnsEmptyList()
	{
		var titles = HandBrakeParser.ParseScan("");

		Assert.Empty(titles);
	}

	[Fact]
	public void ParseScan_ValidJson_ParsesTitles()
	{
		var json = """
		{
			"TitleList": [
				{
					"Index": 1,
					"Duration": { "Hours": 1, "Minutes": 30, "Seconds": 45 },
					"Geometry": { "Width": 1920, "Height": 1080 },
					"FrameRate": { "Num": 24000, "Den": 1001 },
					"VideoCodec": "H.264",
					"AudioList": [
						{
							"CodecName": "AAC",
							"LanguageCode": "eng",
							"SampleRate": 48000,
							"BitRate": 160000,
							"ChannelLayoutName": "5.1"
						}
					],
					"SubtitleList": [
						{
							"LanguageCode": "eng",
							"SourceName": "SRT"
						}
					],
					"ChapterList": [
						{
							"Name": "Chapter 1",
							"Duration": { "Hours": 0, "Minutes": 15, "Seconds": 0 }
						},
						{
							"Name": "Chapter 2",
							"Duration": { "Hours": 0, "Minutes": 20, "Seconds": 0 }
						}
					]
				}
			]
		}
		""";

		var titles = HandBrakeParser.ParseScan(json);

		Assert.Single(titles);
		var title = titles[0];
		Assert.Equal(1, title.TitleNumber);
		Assert.Equal(new TimeSpan(1, 30, 45), title.Duration);
		Assert.Equal(1920, title.Width);
		Assert.Equal(1080, title.Height);
		Assert.True(title.FrameRate > 23.9 && title.FrameRate < 24.0);

		Assert.Single(title.VideoStreams);
		Assert.Equal("H.264", title.VideoStreams[0].Codec);

		Assert.Single(title.AudioTracks);
		Assert.Equal("AAC", title.AudioTracks[0].Codec);
		Assert.Equal("eng", title.AudioTracks[0].Language);

		Assert.Single(title.Subtitles);
		Assert.Equal("eng", title.Subtitles[0].Language);

		Assert.Equal(2, title.Chapters.Count);
		Assert.Equal("Chapter 1", title.Chapters[0].Name);
	}

	[Fact]
	public void ParseScan_TextFallback_ParsesTitleBlock()
	{
		var text = """
		+ title 1:
		  + duration: 01:30:45
		  + size: 1920x1080
		+ title 2:
		  + duration: 00:05:00
		  + size: 720x480
		""";

		var titles = HandBrakeParser.ParseScan(text);

		Assert.Equal(2, titles.Count);
		Assert.Equal(1, titles[0].TitleNumber);
		Assert.Equal(new TimeSpan(1, 30, 45), titles[0].Duration);
		Assert.Equal(1920, titles[0].Width);
		Assert.Equal(1080, titles[0].Height);
		Assert.Equal(2, titles[1].TitleNumber);
	}
}
