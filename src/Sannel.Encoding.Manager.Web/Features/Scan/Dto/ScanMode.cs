namespace Sannel.Encoding.Manager.Web.Features.Scan.Dto;

/// <summary>
/// The browsing mode for scan results.
/// </summary>
public enum ScanMode
{
	/// <summary>
	/// Show all titles on the disc filtered by minimum duration.
	/// </summary>
	Titles = 0,

	/// <summary>
	/// Show chapters for a selected title.
	/// </summary>
	Chapters = 1
}
