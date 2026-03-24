namespace Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;

/// <summary>
/// Represents a labeled root directory configuration.
/// </summary>
public class RootDirectory
{
	/// <summary>
	/// Gets or sets the user-facing label for this root directory.
	/// </summary>
	public required string Label { get; set; }

	/// <summary>
	/// Gets or sets the physical filesystem path for this root directory.
	/// </summary>
	public required string Path { get; set; }
}
