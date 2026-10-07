namespace Sannel.Encoding.Manager.Web.Features.DiscMenu.Dto;

/// <summary>State of a disc menu inspection.</summary>
public enum DiscMenuJobState
{
	Inspecting,
	Completed,
	Failed,

	/// <summary>Menu inspection cannot run on this host (feature disabled, probe or native library missing).</summary>
	Unavailable,
}
