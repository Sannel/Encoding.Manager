namespace Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;

/// <summary>
/// Response containing information about a configured root directory.
/// </summary>
public class ConfiguredDirectoryResponse
{
	/// <summary>
	/// Gets or sets the label for this root directory.
	/// </summary>
	public required string Label { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the directory exists and is accessible.
	/// </summary>
	public required bool Exists { get; set; }
}
