namespace Sannel.Encoding.Runner.Features.Runner.Options;

/// <summary>Configuration for filesystem roots on this machine.</summary>
public class FilesystemOptions
{
	public List<RootEntry> Roots { get; set; } = [];
}

public class RootEntry
{
	/// <summary>Label matching the web app's configured root label.</summary>
	public string Label { get; set; } = string.Empty;

	/// <summary>Absolute native OS path to the root directory on this machine.</summary>
	public string Path { get; set; } = string.Empty;
}
