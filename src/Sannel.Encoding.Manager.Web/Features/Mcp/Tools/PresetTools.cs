using System.ComponentModel;
using ModelContextProtocol.Server;
using Sannel.Encoding.Manager.Web.Features.Mcp.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Tools;

/// <summary>MCP tools for HandBrake presets.</summary>
[McpServerToolType]
public class PresetTools
{
	private readonly IPresetService _presetService;

	public PresetTools(IPresetService presetService) =>
		this._presetService = presetService;

	[McpServerTool(Name = "list_presets", ReadOnly = true, Idempotent = true)]
	[Description("Lists the HandBrake presets configured on the server. Pass a label as presetLabel to queue_encode_job.")]
	public async Task<IReadOnlyList<McpPresetDto>> ListPresetsAsync(CancellationToken ct) =>
		(await this._presetService.GetPresetsAsync(ct))
			.Select(p => new McpPresetDto { Label = p.Label, PresetName = p.PresetName })
			.ToList();
}
