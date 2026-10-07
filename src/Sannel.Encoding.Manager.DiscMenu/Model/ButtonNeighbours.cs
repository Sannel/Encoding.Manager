namespace Sannel.Encoding.Manager.DiscMenu.Model;

/// <summary>Which button the arrow keys move to from a button. Null = stays put.</summary>
public class ButtonNeighbours
{
	public int? Up { get; set; }

	public int? Down { get; set; }

	public int? Left { get; set; }

	public int? Right { get; set; }
}
