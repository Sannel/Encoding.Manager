using Sannel.Encoding.Runner.Features.Encoding;

namespace Sannel.Encoding.Runner.Tests.Features.Encoding;

public sealed class HandBrakeArgBuilderTests
{
	[Theory]
	[InlineData(1, "--angle 1")]
	[InlineData(2, "--angle 2")]
	[InlineData(3, "--angle 3")]
	public void BuildAngleArg_ValidAngle_ReturnsExpected(int angleNumber, string expected)
	{
		var result = HandBrakeArgBuilder.BuildAngleArg(angleNumber);

		Assert.Equal(expected, result);
	}

	[Fact]
	public void BuildTitleArg_ReturnsExpected()
	{
		var result = HandBrakeArgBuilder.BuildTitleArg(5);

		Assert.Equal("--title 5", result);
	}

	[Fact]
	public void BuildChapterArgs_BothSet_ReturnsRange()
	{
		var result = HandBrakeArgBuilder.BuildChapterArgs(2, 4);

		Assert.Equal("--chapters 2-4", result);
	}

	[Fact]
	public void BuildChapterArgs_NeitherSet_ReturnsEmpty()
	{
		var result = HandBrakeArgBuilder.BuildChapterArgs(null, null);

		Assert.Equal(string.Empty, result);
	}

	[Fact]
	public void CombineArgs_SkipsEmptyArgs()
	{
		var result = HandBrakeArgBuilder.CombineArgs("--title 1", string.Empty, "--angle 2");

		Assert.Equal("--title 1 --angle 2", result);
	}
}
