namespace Sannel.Encoding.Manager.DiscMenu.Model;

/// <summary>One menu screen on the disc.</summary>
public class MenuNode
{
	/// <summary>Stable id within the map (m0, m1, …).</summary>
	public string Id { get; set; } = string.Empty;

	public MenuKind Kind { get; set; }

	/// <summary>"VMGM" / "VTSM" (DVD) or "HDMV" / "BD-J" (Blu-ray).</summary>
	public string Domain { get; set; } = string.Empty;

	/// <summary>DVD title set number for VTSM menus.</summary>
	public int? TitleSet { get; set; }

	/// <summary>Key presses from disc start that reach this menu.</summary>
	public List<NavKey> ReachPath { get; set; } = [];

	/// <summary>The menu this one was first reached from.</summary>
	public string? ParentMenuId { get; set; }

	/// <summary>Disc video size the button rectangles are in.</summary>
	public int FrameWidth { get; set; }

	public int FrameHeight { get; set; }

	/// <summary>Display aspect ratio of the menu video (e.g. 1.333 for 4:3, 1.778 for 16:9).</summary>
	public double DisplayAspectRatio { get; set; }

	public MenuScreenshot Screenshot { get; set; } = new();

	public List<MenuButton> Buttons { get; set; } = [];
}
