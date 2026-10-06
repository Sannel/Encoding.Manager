namespace Sannel.Encoding.Manager.Web.Features.Interlace.Dto;

/// <summary>Whether a title or file needs deinterlacing (decomb) before encoding.</summary>
public enum InterlaceVerdict
{
	/// <summary>Interlaced video (or a DVD, which is always treated as interlaced).</summary>
	Interlaced,

	/// <summary>Telecined film (3:2 pulldown / repeated fields).</summary>
	Telecined,

	/// <summary>Both interlaced and telecined frames, or HandBrake saw combing that the ffmpeg sample did not.</summary>
	Mixed,

	/// <summary>Progressive — no decomb needed.</summary>
	Progressive,

	/// <summary>A probe is queued or running.</summary>
	Pending,

	/// <summary>Could not be determined (ffmpeg unavailable, probe failed, or too few decidable frames).</summary>
	Unknown,
}
