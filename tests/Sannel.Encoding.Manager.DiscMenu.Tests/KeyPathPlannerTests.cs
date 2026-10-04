using Sannel.Encoding.Manager.DiscMenu;
using Sannel.Encoding.Manager.DiscMenu.Model;

namespace Sannel.Encoding.Manager.DiscMenu.Tests;

public class KeyPathPlannerTests
{
	private static MenuButton Button(int number, int? up = null, int? down = null, int? left = null, int? right = null) => new()
	{
		Number = number,
		Neighbours = new ButtonNeighbours { Up = up, Down = down, Left = left, Right = right },
	};

	[Fact]
	public void Plan_SameButton_ReturnsEmpty()
	{
		var path = KeyPathPlanner.Plan([Button(1)], 1, 1);

		Assert.NotNull(path);
		Assert.Empty(path!);
	}

	[Fact]
	public void Plan_VerticalList_ReturnsDownPresses()
	{
		List<MenuButton> buttons = [Button(1, down: 2), Button(2, up: 1, down: 3), Button(3, up: 2, down: 4), Button(4, up: 3)];

		var path = KeyPathPlanner.Plan(buttons, 1, 4);

		Assert.Equal([NavKey.Down, NavKey.Down, NavKey.Down], path);
	}

	[Fact]
	public void Plan_Grid_ReturnsShortestPath()
	{
		// 1 2
		// 3 4
		List<MenuButton> buttons =
		[
			Button(1, down: 3, right: 2),
			Button(2, down: 4, left: 1),
			Button(3, up: 1, right: 4),
			Button(4, up: 2, left: 3),
		];

		var path = KeyPathPlanner.Plan(buttons, 1, 4);

		Assert.NotNull(path);
		Assert.Equal(2, path!.Count);
	}

	[Fact]
	public void Plan_Unreachable_ReturnsNull()
	{
		List<MenuButton> buttons = [Button(1, down: 2), Button(2, up: 1), Button(3)];

		Assert.Null(KeyPathPlanner.Plan(buttons, 1, 3));
	}
}
