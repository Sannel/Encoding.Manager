namespace Sannel.Encoding.Manager.DiscMenu.Model;

/// <summary>A remote-control key press used to reach a menu from disc start.</summary>
public enum NavKey
{
	Up,
	Down,
	Left,
	Right,
	Enter,

	/// <summary>The disc title / top menu key.</summary>
	TitleMenu,

	/// <summary>The DVD root menu key (or Blu-ray pop-up).</summary>
	RootMenu,

	/// <summary>Blu-ray pop-up menu key.</summary>
	PopUp,
}
