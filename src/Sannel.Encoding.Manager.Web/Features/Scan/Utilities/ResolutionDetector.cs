namespace Sannel.Encoding.Manager.Web.Features.Scan.Utilities;

/// <summary>
/// Utility to detect and normalize video resolution based on width/height.
/// </summary>
public static class ResolutionDetector
{
	private static readonly (int width, int height, string label)[] ResolutionLevels =
	[
		(3840, 2160, "4k"),
		(1920, 1080, "1080p"),
		(1280, 720, "720p"),
		(854, 480, "480p"),
		(640, 480, "480p"),
		(720, 480, "480p"),
	];

	/// <summary>
	/// Detect the closest resolution label based on width and height.
	/// Returns the label for the closest resolution (e.g., "1080p", "720p").
	/// </summary>
	public static string DetectResolution(int width, int height)
	{
		if (width <= 0 || height <= 0)
		{
			return string.Empty;
		}

		// Calculate pixel count
		var pixelCount = width * height;

		// Find the resolution with closest pixel count
		var closest = ResolutionLevels[0];
		var closestDiff = long.MaxValue;

		foreach (var (w, h, label) in ResolutionLevels)
		{
			var resPixelCount = w * h;
			var diff = Math.Abs(pixelCount - resPixelCount);

			if (diff < closestDiff)
			{
				closestDiff = diff;
				closest = (w, h, label);
			}
		}

		return closest.label;
	}

	/// <summary>
	/// Get the standard resolution labels available for selection.
	/// </summary>
	public static IReadOnlyList<string> GetAvailableResolutions() =>
	[
		"4k",
		"1080p",
		"720p",
		"480p"
	];
}
