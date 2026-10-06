using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.Web.Features.Interlace.Dto;
using Sannel.Encoding.Manager.Web.Features.Interlace.Options;
using Sannel.Encoding.Manager.Web.Features.Interlace.Services;

namespace Sannel.Encoding.Manager.DiscMenu.Tests;

public class InterlaceTests
{
	// Real ffmpeg 7 output: the summary is printed twice, once from an empty (reinitialised) filter instance.
	private const string RealIdetOutput = """
		  Duration: 00:03:08.27, start: 0.000000, bitrate: 1093 kb/s
		[Parsed_idet_0 @ 0x557e053ee000] Repeated Fields: Neither:     0 Top:     0 Bottom:     0
		[Parsed_idet_0 @ 0x557e053ee000] Single frame detection: TFF:     0 BFF:     0 Progressive:     0 Undetermined:     0
		[Parsed_idet_0 @ 0x557e053ee000] Multi frame detection: TFF:     0 BFF:     0 Progressive:     0 Undetermined:     0
		[Parsed_idet_0 @ 0x767178002c80] Repeated Fields: Neither:   168 Top:    16 Bottom:    17
		[Parsed_idet_0 @ 0x767178002c80] Single frame detection: TFF:   154 BFF:     0 Progressive:    36 Undetermined:    11
		[Parsed_idet_0 @ 0x767178002c80] Multi frame detection: TFF:   197 BFF:     0 Progressive:     4 Undetermined:     0
		""";

	private static InterlaceClassifier Classifier() =>
		new(Options.Create(new InterlaceOptions()), Options.Create(new PresetDefaultsOptions()));

	[Fact]
	public void Parse_SumsMultiFrameAndRepeatedFieldLines()
	{
		var counts = IdetParser.Parse(RealIdetOutput);

		Assert.Equal(197, counts.Tff);
		Assert.Equal(0, counts.Bff);
		Assert.Equal(4, counts.Progressive);
		Assert.Equal(168, counts.RepeatedNeither);
		Assert.Equal(33, counts.RepeatedTop + counts.RepeatedBottom);
	}

	[Fact]
	public void ParseDuration_ReadsInputHeader() =>
		Assert.Equal(TimeSpan.FromSeconds(188.27), IdetParser.ParseDuration(RealIdetOutput));

	[Theory]
	[InlineData(0, 0, 500, 500, 0, InterlaceVerdict.Progressive)]
	[InlineData(450, 0, 50, 500, 0, InterlaceVerdict.Interlaced)]
	[InlineData(0, 0, 500, 400, 100, InterlaceVerdict.Telecined)]
	[InlineData(300, 0, 200, 400, 100, InterlaceVerdict.Mixed)]
	[InlineData(5, 0, 20, 25, 0, InterlaceVerdict.Unknown)]
	public void Measure_AppliesThresholds(int tff, int bff, int progressive, int neither, int repeated, InterlaceVerdict expected)
	{
		var counts = new IdetCounts(tff, bff, progressive, 0, neither, repeated, 0);

		Assert.Equal(expected, Classifier().Measure(counts).Verdict);
	}

	[Fact]
	public void Dvd_IsAlwaysInterlacedWithDecombPreset()
	{
		var result = Classifier().ForDvd();

		Assert.Equal(InterlaceVerdict.Interlaced, result.Verdict);
		Assert.Equal("dvd", result.Source);
		Assert.Equal("4K AV1 Decomb", result.RecommendedPreset);
	}

	[Fact]
	public void BlurayTitle_ProgressiveButHandBrakeSawCombing_IsMixed()
	{
		var measurement = new InterlaceMeasurement(InterlaceVerdict.Progressive, 2, 0, 1500);

		var result = Classifier().ForBlurayTitle(measurement, handBrakeDetected: true);

		Assert.Equal(InterlaceVerdict.Mixed, result.Verdict);
		Assert.Equal("handbrake+ffmpeg", result.Source);
		Assert.Equal("4K AV1 Decomb", result.RecommendedPreset);
	}

	[Fact]
	public void BlurayTitle_WithoutFfmpeg_FallsBackToHandBrakeFlag()
	{
		var classifier = Classifier();

		Assert.Equal(InterlaceVerdict.Interlaced, classifier.ForBlurayTitle(null, true).Verdict);
		var progressive = classifier.ForBlurayTitle(null, false);
		Assert.Equal(InterlaceVerdict.Progressive, progressive.Verdict);
		Assert.Equal("handbrake", progressive.Source);
		Assert.Equal("4K AV1", progressive.RecommendedPreset);
	}

	[Fact]
	public void File_WithoutMeasurement_IsUnknownWithNoPreset()
	{
		var result = Classifier().ForFile(null);

		Assert.Equal(InterlaceVerdict.Unknown, result.Verdict);
		Assert.Null(result.RecommendedPreset);
	}

	[Fact]
	public void SamplePoints_UseDurationFractionsAndStartShortSourcesAtZero()
	{
		var points = InterlaceProbeService.SamplePoints([0.10, 0.45, 0.80], TimeSpan.FromSeconds(1000)).ToList();
		Assert.Equal([TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(450), TimeSpan.FromSeconds(800)], points);

		var shortPoints = InterlaceProbeService.SamplePoints([0.10, 0.45, 0.80], TimeSpan.FromSeconds(20)).ToList();
		Assert.Equal([TimeSpan.Zero], shortPoints);
	}
}
