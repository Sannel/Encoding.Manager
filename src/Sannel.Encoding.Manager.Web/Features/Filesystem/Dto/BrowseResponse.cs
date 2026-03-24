namespace Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;

/// <summary>
/// Response containing the contents of a browsed directory.
/// </summary>
public class BrowseResponse
{
	/// <summary>
	/// Gets or sets the list of subdirectories in the current location.
	/// </summary>
	public required List<DirectoryEntryResponse> Directories { get; set; }

	/// <summary>
	/// Gets or sets the list of media files (.mp4, .mkv) in the current location.
	/// </summary>
	public required List<FileEntryResponse> Files { get; set; }
}
