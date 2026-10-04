namespace Sannel.Encoding.Manager.DiscMenu.Model;

/// <summary>What pressing a menu button does.</summary>
public enum ButtonActionType
{
	/// <summary>Opens another menu (<see cref="ButtonAction.MenuId"/>).</summary>
	OpenMenu,

	/// <summary>Starts playback of a title / chapter range.</summary>
	PlayTitle,

	/// <summary>Changes an audio, subtitle or angle setting and stays on the menu.</summary>
	ChangeSetting,

	/// <summary>Could not be determined (see <see cref="ButtonAction.Reason"/>).</summary>
	Unknown,
}
