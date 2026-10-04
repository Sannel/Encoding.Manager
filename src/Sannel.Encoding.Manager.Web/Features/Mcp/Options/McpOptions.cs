namespace Sannel.Encoding.Manager.Web.Features.Mcp.Options;

/// <summary>Configuration for the MCP server (config section <c>Mcp</c>).</summary>
public class McpOptions
{
	/// <summary>When false, <c>/mcp</c> is not mapped and the AI Access page shows a disabled notice.</summary>
	public bool Enabled { get; set; } = true;

	/// <summary>Server name reported to MCP clients.</summary>
	public string ServerName { get; set; } = "sannel-encoding-manager";

	/// <summary>How long scan and menu tools wait for a background job before returning "still running".</summary>
	public int ScanWaitSeconds { get; set; } = 5;

	/// <summary>Forced rescans allowed per key per window.</summary>
	public int ForcedRescanPermitLimit { get; set; } = 3;

	/// <summary>Length of the forced-rescan rate-limit window, in minutes.</summary>
	public int ForcedRescanWindowMinutes { get; set; } = 10;
}
