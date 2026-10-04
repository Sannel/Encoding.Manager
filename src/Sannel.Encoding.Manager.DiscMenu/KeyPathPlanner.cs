using Sannel.Encoding.Manager.DiscMenu.Model;

namespace Sannel.Encoding.Manager.DiscMenu;

/// <summary>Finds the shortest arrow-key sequence that moves the menu highlight from one button to another.</summary>
public static class KeyPathPlanner
{
	private static readonly NavKey[] _directions = [NavKey.Up, NavKey.Down, NavKey.Left, NavKey.Right];

	/// <summary>
	/// Breadth-first search over the buttons' neighbour links. Returns the arrow keys to press (empty when
	/// <paramref name="from"/> equals <paramref name="to"/>), or null when <paramref name="to"/> is unreachable.
	/// </summary>
	public static IReadOnlyList<NavKey>? Plan(IReadOnlyList<MenuButton> buttons, int from, int to)
	{
		if (from == to)
		{
			return [];
		}

		var byNumber = buttons.ToDictionary(b => b.Number);
		var previous = new Dictionary<int, (int From, NavKey Key)>();
		var queue = new Queue<int>();
		queue.Enqueue(from);
		var seen = new HashSet<int> { from };

		while (queue.Count > 0)
		{
			var current = queue.Dequeue();
			if (!byNumber.TryGetValue(current, out var button) || button.Neighbours is null)
			{
				continue;
			}

			foreach (var direction in _directions)
			{
				var next = direction switch
				{
					NavKey.Up => button.Neighbours.Up,
					NavKey.Down => button.Neighbours.Down,
					NavKey.Left => button.Neighbours.Left,
					_ => button.Neighbours.Right,
				};
				if (next is not { } n || !seen.Add(n))
				{
					continue;
				}

				previous[n] = (current, direction);
				if (n == to)
				{
					return Unwind(previous, from, to);
				}

				queue.Enqueue(n);
			}
		}

		return null;
	}

	private static List<NavKey> Unwind(Dictionary<int, (int From, NavKey Key)> previous, int from, int to)
	{
		var keys = new List<NavKey>();
		var node = to;
		while (node != from)
		{
			var (prior, key) = previous[node];
			keys.Add(key);
			node = prior;
		}

		keys.Reverse();
		return keys;
	}
}
