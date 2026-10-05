namespace Sannel.Encoding.Manager.DiscMenu.Model;

/// <summary>One button on a disc menu.</summary>
public class MenuButton
{
	/// <summary>Button number (1-based); the number drawn on annotated screenshots.</summary>
	public int Number { get; set; }

	public ButtonRect Rect { get; set; } = new();

	public ButtonNeighbours? Neighbours { get; set; }

	/// <summary>True when this button is highlighted when the menu appears.</summary>
	public bool IsDefault { get; set; }

	public ButtonAction Action { get; set; } = new() { Type = ButtonActionType.Unknown };

	/// <summary>Arrow keys from the menu's default highlight to this button (null when unreachable by keys).</summary>
	public List<NavKey>? FocusPath { get; set; }

	/// <summary>Screenshot of the menu with this button highlighted.</summary>
	public MenuScreenshot? Screenshot { get; set; }
}
