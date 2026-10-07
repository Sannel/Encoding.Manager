using Sannel.Encoding.Manager.DiscMenu;
using Sannel.Encoding.Manager.DiscMenu.Dvd;
using Sannel.Encoding.Manager.DiscMenu.Model;
using Sannel.Encoding.Manager.DiscMenu.Native;

namespace Sannel.Encoding.Manager.DiscMenu.Tests;

/// <summary>
/// Runs the real libdvdnav crawler against a DVD folder named by the DISC_MENU_TEST_DVD environment variable.
/// The expected structure is the authored test disc: a main menu (Play All / Episodes / Extras) and an
/// Episodes submenu whose buttons play chapters 1-2, 3-4 and 5-6 of title 1. Returns early when not configured.
/// </summary>
public class DvdMenuCrawlerIntegrationTests
{
	[Fact]
	public void Crawl_AuthoredTestDisc_MapsEpisodeChapterRanges()
	{
		var path = Environment.GetEnvironmentVariable("DISC_MENU_TEST_DVD");
		if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
		{
			return;
		}

		NativeLibraryResolver.Install(null);
		var map = new DvdMenuCrawler(new CrawlOptions()).Crawl(path);

		Assert.True(map.Complete);
		Assert.Equal(2, map.Menus.Count);
		var episodes = map.Menus.Single(m => m.Buttons.Count == 4);
		Assert.Equal([NavKey.Down, NavKey.Enter], episodes.ReachPath);
		var ranges = episodes.Buttons.Take(3).Select(b => (b.Action.DvdTitle, b.Action.StartChapter, b.Action.EndChapter)).ToList();
		Assert.Equal([(1, 1, 2), (1, 3, 4), (1, 5, 6)], ranges);
		Assert.All(episodes.Buttons.Take(3), b => Assert.Equal("ReturnToMenu", b.Action.Then));
		Assert.Equal(ButtonActionType.OpenMenu, episodes.Buttons[3].Action.Type);
	}
}
