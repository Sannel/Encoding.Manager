namespace Sannel.Encoding.Manager.DiscMenu.Model;

/// <summary>How the target of a button was determined.</summary>
public enum ActionConfidence
{
	/// <summary>Determined by running the disc navigation logic (DVD).</summary>
	Exact,

	/// <summary>Observed from playback events (Blu-ray HDMV).</summary>
	Observed,

	/// <summary>Inferred; the disc logic could not be followed to the end (Blu-ray BD-J).</summary>
	Inferred,
}
