using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Services;

namespace Sannel.Encoding.Manager.DiscMenu.Tests;

public class AiHandoffTextTests
{
	[Fact]
	public void ForDisc_IncludesLocationAndMenuTools()
	{
		var text = AiHandoffText.ForDisc("Q", "Rips/Show S1D1", DiscType.BluRay);

		Assert.Contains("Blu-ray disc folder", text);
		Assert.Contains("- root: \"Q\"", text);
		Assert.Contains("- path: \"Rips/Show S1D1\"", text);
		Assert.Contains("inspect_disc_menus(root: \"Q\", path: \"Rips/Show S1D1\")", text);
		Assert.Contains("selection \"disc\"", text);
	}

	[Fact]
	public void ForFolder_AtRoot_UsesEmptyPath()
	{
		var text = AiHandoffText.ForFolder("G", null);

		Assert.Contains("- path: \"\"", text);
		Assert.Contains("shown in the app as: G\n", text.Replace("\r\n", "\n") + "\n");
		Assert.Contains("selection \"folder\"", text);
	}

	[Fact]
	public void ForFile_IncludesSizeAndFileSelection()
	{
		var text = AiHandoffText.ForFile("Q", "Movies/Film.mkv", 3L * 1024 * 1024 * 1024);

		Assert.Contains("3.00 GB", text);
		Assert.Contains("selection \"file\"", text);
	}
}
