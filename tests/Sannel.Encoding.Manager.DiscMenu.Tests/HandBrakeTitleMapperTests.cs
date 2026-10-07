using Sannel.Encoding.Manager.DiscMenu.Model;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Manager.Web.Features.DiscMenu.Services;

namespace Sannel.Encoding.Manager.DiscMenu.Tests;

public class HandBrakeTitleMapperTests
{
	private static DiscMenuMap MapWith(string discType, ButtonAction action) => new()
	{
		DiscType = discType,
		Menus = [new MenuNode { Id = "m0", Buttons = [new MenuButton { Number = 1, Action = action }] }],
		Titles = [new DiscTitle { DvdTitle = action.DvdTitle, Playlist = action.Playlist }],
	};

	[Fact]
	public void Fill_Dvd_UsesDvdTitleWhenScanned()
	{
		var map = MapWith("DVD", new ButtonAction { Type = ButtonActionType.PlayTitle, DvdTitle = 2 });

		HandBrakeTitleMapper.Fill(map, [new TitleInfo { TitleNumber = 1 }, new TitleInfo { TitleNumber = 2 }]);

		Assert.Equal(2, map.Menus[0].Buttons[0].Action.HandBrakeTitle);
		Assert.Equal(2, map.Titles[0].HandBrakeTitle);
	}

	[Fact]
	public void Fill_Dvd_TitleMissingFromScan_LeavesNull()
	{
		var map = MapWith("DVD", new ButtonAction { Type = ButtonActionType.PlayTitle, DvdTitle = 5 });

		HandBrakeTitleMapper.Fill(map, [new TitleInfo { TitleNumber = 1 }]);

		Assert.Null(map.Menus[0].Buttons[0].Action.HandBrakeTitle);
	}

	[Fact]
	public void Fill_Bluray_MatchesPlaylist()
	{
		var map = MapWith("BluRay", new ButtonAction { Type = ButtonActionType.PlayTitle, Playlist = 800 });

		HandBrakeTitleMapper.Fill(map, [new TitleInfo { TitleNumber = 1, Playlist = 5 }, new TitleInfo { TitleNumber = 3, Playlist = 800 }]);

		Assert.Equal(3, map.Menus[0].Buttons[0].Action.HandBrakeTitle);
	}

	[Fact]
	public void Fill_BlurayWithoutScan_AddsWarning()
	{
		var map = MapWith("BluRay", new ButtonAction { Type = ButtonActionType.PlayTitle, Playlist = 800 });

		HandBrakeTitleMapper.Fill(map, null);

		Assert.Null(map.Menus[0].Buttons[0].Action.HandBrakeTitle);
		Assert.Single(map.Warnings);
	}
}
