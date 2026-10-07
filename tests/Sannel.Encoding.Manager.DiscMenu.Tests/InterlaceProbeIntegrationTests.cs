using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Manager.Web.Features.Interlace.Dto;
using Sannel.Encoding.Manager.Web.Features.Interlace.Options;
using Sannel.Encoding.Manager.Web.Features.Interlace.Services;

namespace Sannel.Encoding.Manager.DiscMenu.Tests;

/// <summary>
/// Runs the real ffmpeg idet probe. Configure with environment variables (each test returns early when unset):
/// INTERLACE_TEST_INTERLACED_FILE (an interlaced media file, e.g. a DVD-sourced extra),
/// INTERLACE_TEST_BLURAY + INTERLACE_TEST_BLURAY_PLAYLIST (a Blu-ray folder and a progressive playlist).
/// </summary>
public class InterlaceProbeIntegrationTests
{
	private static InterlaceProbeService CreateProbe()
	{
		var options = Options.Create(new InterlaceOptions());
		var runner = new ProcessRunner();
		var classifier = new InterlaceClassifier(options, Options.Create(new PresetDefaultsOptions()));
		var locator = new FfmpegLocator(runner, options, NullLogger<FfmpegLocator>.Instance);
		return new InterlaceProbeService(runner, locator, classifier, options, NullLogger<InterlaceProbeService>.Instance);
	}

	[Fact]
	public async Task ProbeFile_InterlacedSource_IsInterlaced()
	{
		var file = Environment.GetEnvironmentVariable("INTERLACE_TEST_INTERLACED_FILE");
		if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
		{
			return;
		}

		var measurement = await CreateProbe().ProbeFileAsync(file);

		Assert.NotNull(measurement);
		Assert.True(measurement!.Verdict is InterlaceVerdict.Interlaced or InterlaceVerdict.Mixed, $"got {measurement}");
	}

	[Fact]
	public async Task ProbeBlurayTitle_ProgressivePlaylist_IsProgressive()
	{
		var disc = Environment.GetEnvironmentVariable("INTERLACE_TEST_BLURAY");
		if (string.IsNullOrWhiteSpace(disc) || !Directory.Exists(disc)
			|| !int.TryParse(Environment.GetEnvironmentVariable("INTERLACE_TEST_BLURAY_PLAYLIST"), out var playlist))
		{
			return;
		}

		var measurement = await CreateProbe().ProbeBlurayTitleAsync(disc, playlist, TimeSpan.FromMinutes(20));

		Assert.NotNull(measurement);
		Assert.Equal(InterlaceVerdict.Progressive, measurement!.Verdict);
	}
}
