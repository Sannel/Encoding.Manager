using Sannel.Encoding.Manager.DiscMenu.Model;

namespace Sannel.Encoding.Manager.DiscMenu.Tests;

public class DiscMenuJsonTests
{
	[Fact]
	public void RoundTrip_PreservesMenusAndWritesEnumsAsStrings()
	{
		var map = new DiscMenuMap
		{
			DiscType = "DVD",
			MenuSystem = "DVD",
			Menus =
			[
				new MenuNode
				{
					Id = "m0",
					Kind = MenuKind.Root,
					ReachPath = [NavKey.Down, NavKey.Enter],
					Buttons =
					[
						new MenuButton
						{
							Number = 1,
							Action = new ButtonAction { Type = ButtonActionType.PlayTitle, DvdTitle = 1, StartChapter = 3, EndChapter = 4, Confidence = ActionConfidence.Exact },
						},
					],
				},
			],
		};

		var json = DiscMenuJson.Serialize(map);
		var back = DiscMenuJson.Deserialize(json);

		Assert.Contains("\"PlayTitle\"", json);
		Assert.Contains("\"Enter\"", json);
		Assert.NotNull(back);
		var action = back!.Menus[0].Buttons[0].Action;
		Assert.Equal(ButtonActionType.PlayTitle, action.Type);
		Assert.Equal(3, action.StartChapter);
		Assert.Equal(4, action.EndChapter);
		Assert.Equal([NavKey.Down, NavKey.Enter], back.Menus[0].ReachPath);
	}
}
