namespace Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;

/// <summary>
/// Indicates the type of disc content detected in a directory.
/// </summary>
public enum DiscType
{
	/// <summary>
	/// No disc structure detected.
	/// </summary>
	None = 0,

	/// <summary>
	/// Blu-ray disc structure detected (BDMV folder with index.bdmv or STREAM folder).
	/// </summary>
	BluRay = 1,

	/// <summary>
	/// DVD disc structure detected (VIDEO_TS folder with VIDEO_TS.IFO).
	/// </summary>
	DVD = 2
}
