using System.Text;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Filesystem.Services;

/// <summary>
/// Builds the text the Filesystem Browser's "Copy for AI" button puts on the clipboard: where the item is (root
/// label + root-relative path, the exact values the MCP tools take) and which tools to use for that kind of item.
/// </summary>
public static class AiHandoffText
{
	private const string ServerName = "sannel-encoding";

	/// <summary>A disc folder (DVD VIDEO_TS or Blu-ray BDMV rip).</summary>
	public static string ForDisc(string root, string path, DiscType discType)
	{
		var kind = discType == DiscType.BluRay ? "Blu-ray" : "DVD";
		var text = Header(root, path, $"{kind} disc folder");
		text.AppendLine("Suggested steps:");
		text.AppendLine($"1. scan_disc(root: \"{root}\", path: \"{path}\") — poll get_scan_status while it says \"Scanning\".");
		text.AppendLine($"2. inspect_disc_menus(root: \"{root}\", path: \"{path}\") — poll get_disc_menu_status while it says \"Inspecting\".");
		text.AppendLine("3. get_disc_menu_screenshot(annotated: true) for the main and episode/scene menus; read the button labels and use each button's action (handBrakeTitle, startChapter, endChapter) to work out which titles or chapter ranges are which episodes or features.");
		text.AppendLine("4. Look up names (tvdb_get_episodes for TV, omdb_search_movie for movies) and pick a preset with list_presets.");
		text.AppendLine($"5. Show me the plan, then queue_encode_job with root \"{root}\", path \"{path}\", selection \"disc\" and mode \"Titles\" or \"Chapters\".");
		return text.ToString().TrimEnd();
	}

	/// <summary>A folder of media files (queued as one "Files" job).</summary>
	public static string ForFolder(string root, string? path)
	{
		var normalized = path ?? string.Empty;
		var text = Header(root, normalized, normalized.Length == 0 ? "media root folder" : "folder of media files");
		text.AppendLine("Suggested steps:");
		text.AppendLine($"1. list_folder_media_files(root: \"{root}\", path: \"{normalized}\") to see every .mkv/.mp4 under it (relativePath values).");
		text.AppendLine("2. Work out which file is which episode or movie from the file names and sizes, and look up names (tvdb_get_episodes / omdb_search_movie).");
		text.AppendLine("3. Pick a preset with list_presets.");
		text.AppendLine($"4. Show me the plan, then queue_encode_job with root \"{root}\", path \"{normalized}\", selection \"folder\", one track per file (sourceRelativePath).");
		return text.ToString().TrimEnd();
	}

	/// <summary>A single media file.</summary>
	public static string ForFile(string root, string path, long sizeBytes)
	{
		var text = Header(root, path, $"single media file ({FormatSize(sizeBytes)})");
		text.AppendLine("Suggested steps:");
		text.AppendLine("1. Work out what it is from the file name and folder, and look up the name (tvdb_get_episodes / omdb_search_movie).");
		text.AppendLine("2. Pick a preset with list_presets.");
		text.AppendLine($"3. Show me the plan, then queue_encode_job with root \"{root}\", path \"{path}\", selection \"file\" and exactly one track.");
		return text.ToString().TrimEnd();
	}

	private static StringBuilder Header(string root, string path, string kind)
	{
		var text = new StringBuilder();
		text.AppendLine($"Use the {ServerName} MCP server to set up encodes for this {kind}.");
		text.AppendLine();
		text.AppendLine($"- root: \"{root}\"");
		text.AppendLine($"- path: \"{path}\"");
		text.AppendLine($"- shown in the app as: {root}{(path.Length == 0 ? string.Empty : "/" + path)}");
		text.AppendLine();
		return text;
	}

	private static string FormatSize(long bytes)
	{
		string[] units = ["B", "KB", "MB", "GB", "TB"];
		var size = (double)bytes;
		var unit = 0;
		while (size >= 1024 && unit < units.Length - 1)
		{
			size /= 1024;
			unit++;
		}

		return $"{size:F2} {units[unit]}";
	}
}
