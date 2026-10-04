namespace Sannel.Encoding.Manager.Web.Features.Mcp;

/// <summary>The workflow guide sent to MCP clients on initialize.</summary>
public static class McpServerInstructions
{
	public const string Text = """
		Sannel Encoding Manager sets up HandBrake encode jobs for ripped DVDs, Blu-rays and media files.

		Workflow:
		1. list_roots, then browse_directory to find the disc folder (discType DVD/BluRay), media folder, or file.
		2. Discs: scan_disc (if status is "Scanning", wait pollAfterSeconds and call get_scan_status until "Completed").
		   Folders: list_folder_media_files.
		3. Discs: inspect_disc_menus (poll get_disc_menu_status while "Inspecting"), then get_disc_menu_screenshot with
		   annotated=true for the relevant menus. Read the button labels in the image and use each button's action
		   (handBrakeTitle, startChapter, endChapter) to decide which titles or chapter ranges are which episode or feature.
		4. Names: TV → tvdb_list_cached_series / tvdb_get_episodes (episode name = outputName, plus seasonNumber and
		   episodeNumber). Movies → omdb_search_movie (title = outputName, year = movieYear, resolution per track from
		   scan_disc's detectedResolution).
		5. list_presets, then queue_encode_job with a presetLabel. Use mode "Chapters" when one title holds several
		   episodes (one track per chapter range), otherwise mode "Titles".
		6. get_queue to confirm.

		Rules: tracks with a blank outputName are skipped. Output names cannot contain / \ : * ? " < > |.
		Do not use forceRescan or forceRefresh unless the user asks — they are slow and rate-limited.
		Confirm the plan (which titles become which files) with the user before queuing when it is ambiguous.
		""";
}
