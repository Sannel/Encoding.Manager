using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Filesystem.Options;

/// <summary>
/// Configuration options for the Filesystem feature.
/// </summary>
public class FilesystemOptions
{
	/// <summary>
	/// Gets or sets the collection of configured root directories.
	/// </summary>
	public List<RootDirectory> Roots { get; set; } = new();
}
