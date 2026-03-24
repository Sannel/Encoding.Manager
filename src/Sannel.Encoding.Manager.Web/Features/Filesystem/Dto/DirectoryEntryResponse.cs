namespace Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;

/// <summary>
/// Represents a directory entry in a browse response.
/// </summary>
public class DirectoryEntryResponse
{
	/// <summary>
	/// Gets or sets the name of the directory.
	/// </summary>
	public required string Name { get; set; }

	/// <summary>
	/// Gets or sets the disc type detected in this directory.
	/// </summary>
	public required DiscType DiscType { get; set; }
}
