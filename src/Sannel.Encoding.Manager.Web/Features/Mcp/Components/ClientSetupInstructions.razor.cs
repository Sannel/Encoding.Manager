using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Components;

/// <summary>Copy-paste MCP client configuration for common AI clients.</summary>
public partial class ClientSetupInstructions : ComponentBase
{
	private const string KeyPlaceholder = "<YOUR_API_KEY>";
	private const string ServerName = "sannel-encoding";

	[Inject]
	private IJSRuntime JS { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	/// <summary>The full MCP endpoint URL.</summary>
	[Parameter]
	[EditorRequired]
	public string EndpointUrl { get; set; } = string.Empty;

	/// <summary>The plaintext key during the one-time reveal; null otherwise.</summary>
	[Parameter]
	public string? ApiKey { get; set; }

	private bool HasKey => !string.IsNullOrEmpty(this.ApiKey);

	private string Key => this.ApiKey ?? KeyPlaceholder;

	private IEnumerable<(string Title, string Help, string Text)> Snippets =>
	[
		(
			"Claude Code",
			"Run this in a terminal. Add --scope user to make it available in every project.",
			$"claude mcp add --transport http {ServerName} {this.EndpointUrl} --header \"Authorization: Bearer {this.Key}\""
		),
		(
			"Claude Desktop",
			"Add this to claude_desktop_config.json (Settings → Developer → Edit Config). It uses the mcp-remote bridge, which needs Node.js.",
			$$"""
			{
			  "mcpServers": {
			    "{{ServerName}}": {
			      "command": "npx",
			      "args": ["-y", "mcp-remote", "{{this.EndpointUrl}}", "--header", "Authorization:${AUTH_HEADER}"],
			      "env": { "AUTH_HEADER": "Bearer {{this.Key}}" }
			    }
			  }
			}
			"""
		),
		(
			"VS Code",
			"Save as .vscode/mcp.json in your workspace (or add to your user MCP configuration).",
			$$"""
			{
			  "servers": {
			    "{{ServerName}}": {
			      "type": "http",
			      "url": "{{this.EndpointUrl}}",
			      "headers": { "Authorization": "Bearer {{this.Key}}" }
			    }
			  }
			}
			"""
		),
		(
			"Other clients",
			"Most MCP clients that support Streamable HTTP accept this shape. The key can also be sent as an X-Api-Key header.",
			$$"""
			{
			  "mcpServers": {
			    "{{ServerName}}": {
			      "type": "http",
			      "url": "{{this.EndpointUrl}}",
			      "headers": { "Authorization": "Bearer {{this.Key}}" }
			    }
			  }
			}
			"""
		),
		(
			"Prompt for your AI",
			"Paste this into a chat once the server is connected.",
			$"Use the {ServerName} MCP server to set up encodes for me. Browse the media roots to find the disc or folder, " +
			"scan it, and for discs inspect the menus and look at the annotated menu screenshots to work out which titles " +
			"or chapter ranges are which episodes or features. Look up the names on TheTVDB (TV) or OMDb (movies), pick a " +
			"preset with list_presets, show me the plan, and once I confirm, queue the jobs with queue_encode_job."
		),
	];

	private async Task CopyAsync(string text)
	{
		await this.JS.InvokeVoidAsync("navigator.clipboard.writeText", text);
		this.Snackbar.Add("Copied to clipboard.", Severity.Success);
	}
}
