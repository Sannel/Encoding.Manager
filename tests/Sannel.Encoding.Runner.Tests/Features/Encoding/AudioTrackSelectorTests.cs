using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Runner.Features.Encoding;

namespace Sannel.Encoding.Runner.Tests.Features.Encoding;

public sealed class AudioTrackSelectorTests
{
	[Fact]
	public void SelectTracks_FirstMatchingTrack_IsCopiedAndStereoEncoded_WhenNotAlreadyAacStereo()
	{
		var tracks = new List<AudioTrackInfo>
		{
			new()
			{
				TrackNumber = 2,
				Language = "eng",
				Codec = "DTS-HD MA",
				ChannelLayout = "5.1",
				SampleRate = 48000,
				Bitrate = 1536000,
			},
			new()
			{
				TrackNumber = 3,
				Language = "eng",
				Codec = "AC3",
				ChannelLayout = "5.1",
				SampleRate = 48000,
				Bitrate = 640000,
			}
		};

		var selected = AudioTrackSelector.SelectTracks(tracks, ["eng"], "Opus");

		Assert.Collection(
			selected,
			first =>
			{
				Assert.Equal(2, first.TrackNumber);
				Assert.Equal("Opus", first.Encoder);
				Assert.Equal("stereo", first.Mixdown);
				Assert.Equal(48000, first.SampleRate);
				Assert.Equal(1536000, first.Bitrate);
			},
			second =>
			{
				Assert.Equal(2, second.TrackNumber);
				Assert.Equal("copy", second.Encoder);
				Assert.Equal("auto", second.Mixdown);
			},
			third =>
			{
				Assert.Equal(3, third.TrackNumber);
				Assert.Equal("copy", third.Encoder);
				Assert.Equal("auto", third.Mixdown);
			});
	}

	[Fact]
	public void SelectTracks_FirstMatchingTrack_IsOnlyCopied_WhenAlreadyAacStereo()
	{
		var tracks = new List<AudioTrackInfo>
		{
			new()
			{
				TrackNumber = 1,
				Language = "eng",
				Codec = "AAC",
				ChannelLayout = "stereo",
				SampleRate = 48000,
				Bitrate = 256000,
			},
			new()
			{
				TrackNumber = 2,
				Language = "eng",
				Codec = "DTS",
				ChannelLayout = "5.1",
				SampleRate = 48000,
				Bitrate = 768000,
			}
		};

		var selected = AudioTrackSelector.SelectTracks(tracks, ["eng"], "Aac");

		Assert.Collection(
			selected,
			first =>
			{
				Assert.Equal(1, first.TrackNumber);
				Assert.Equal("copy", first.Encoder);
				Assert.Equal("auto", first.Mixdown);
			},
			second =>
			{
				Assert.Equal(2, second.TrackNumber);
				Assert.Equal("copy", second.Encoder);
				Assert.Equal("auto", second.Mixdown);
			});
	}
}
