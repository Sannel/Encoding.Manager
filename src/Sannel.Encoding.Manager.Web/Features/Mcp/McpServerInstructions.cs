namespace Sannel.Encoding.Manager.Web.Features.Mcp;

/// <summary>The workflow guide sent to MCP clients on initialize.</summary>
public static class McpServerInstructions
{
	public const string Text = """
		Sannel Encoding Manager sets up HandBrake encode jobs for ripped DVDs, Blu-rays and media files.

		Workflow:
		1. list_roots, then browse_directory to find the disc folder (discType DVD/BluRay), media folder, or file.
		2. Discs: scan_disc (if status is "Scanning", wait pollAfterSeconds and call get_scan_status until "Completed";
		   keep polling while any title's interlace is "pending").
		   Folders: list_folder_media_files (call again after ~20 s while any file's interlace is "pending").
		3. Discs: inspect_disc_menus (poll get_disc_menu_status while "Inspecting"), then get_disc_menu_screenshot with
		   annotated=true for the relevant menus. Read the button labels in the image and use each button's action
		   (handBrakeTitle, startChapter, endChapter) to decide which titles or chapter ranges are which episode or feature.
		4. Names: TV → tvdb_list_cached_series / tvdb_get_episodes (episode name = outputName, plus seasonNumber and
		   episodeNumber). Movies → omdb_search_movie (title = outputName, year = movieYear, resolution per track from
		   scan_disc's detectedResolution).
		5. Presets: every title / file has a recommendedPreset from its interlace verdict (DVD titles are always interlaced;
		   Blu-ray titles and files are checked one by one — extras are often interlaced while the feature is not). Use it
		   per track (tracks[].presetLabel) when they differ, or as the job presetLabel when all agree. Only use another
		   preset when the user names one (list_presets). Then queue_encode_job. Use mode "Chapters" when one title holds
		   several episodes (one track per chapter range), otherwise mode "Titles".
		6. get_queue to confirm.

		Rules: tracks with a blank outputName are skipped. Output names cannot contain / \ : * ? " < > |.
		Do not use forceRescan or forceRefresh unless the user asks — they are slow and rate-limited.
		Confirm the plan (which titles become which files) with the user before queuing when it is ambiguous.
		""";
}
