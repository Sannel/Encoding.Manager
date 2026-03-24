namespace Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;

/// <summary>
/// Represents a file entry in a browse response.
/// </summary>
public class FileEntryResponse
{
	/// <summary>
	/// Gets or sets the name of the file.
	/// </summary>
	public required string Name { get; set; }

	/// <summary>
	/// Gets or sets the file size in bytes.
	/// </summary>
	public required long SizeBytes { get; set; }
}
