namespace Sannel.Encoding.Manager.DiscMenu.Model;

/// <summary>Screenshot availability for a menu.</summary>
public class MenuScreenshot
{
	public bool Available { get; set; }

	/// <summary>Width of the saved PNG.</summary>
	public int Width { get; set; }

	/// <summary>Height of the saved PNG.</summary>
	public int Height { get; set; }

	/// <summary>File name of the raw screenshot inside the screenshot folder.</summary>
	public string? File { get; set; }

	/// <summary>File name of the annotated screenshot inside the screenshot folder.</summary>
	public string? AnnotatedFile { get; set; }

	public string? Error { get; set; }
}
