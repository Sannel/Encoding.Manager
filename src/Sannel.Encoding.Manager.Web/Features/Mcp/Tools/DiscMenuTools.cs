using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Sannel.Encoding.Manager.DiscMenu.Model;
using Sannel.Encoding.Manager.Web.Features.DiscMenu.Dto;
using Sannel.Encoding.Manager.Web.Features.DiscMenu.Services;
using Sannel.Encoding.Manager.Web.Features.Mcp.Dto;
using Sannel.Encoding.Manager.Web.Features.Mcp.Options;
using Sannel.Encoding.Manager.Web.Features.Mcp.Services;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Tools;

/// <summary>MCP tools that expose a disc's menu structure and menu screenshots.</summary>
[McpServerToolType]
public class DiscMenuTools
{
	private readonly IDiscMenuService _discMenuService;
	private readonly IForcedRescanLimiter _limiter;
	private readonly McpCaller _caller;
	private readonly McpOptions _options;

	public DiscMenuTools(IDiscMenuService discMenuService, IForcedRescanLimiter limiter, McpCaller caller, IOptions<McpOptions> options)
	{
		this._discMenuService = discMenuService;
		this._limiter = limiter;
		this._caller = caller;
		this._options = options.Value;
	}

	[McpServerTool(Name = "inspect_disc_menus", ReadOnly = true)]
	[Description("""
		Maps a DVD or Blu-ray disc's menus: every menu and submenu, every button with its on-screen box, and what each
		button does — open another menu (menuId), play a title (handBrakeTitle = scan_disc titleNumber, startChapter,
		endChapter, and what happens after), or change audio/subtitles. Use it to learn which titles or chapter ranges
		are which episodes or features, then read button labels with get_disc_menu_screenshot.
		The first inspection can take a few minutes: if status is "Inspecting", wait pollAfterSeconds and call
		get_disc_menu_status. Results are cached. Only use forceRefresh when the user asks — it is rate-limited.
		""")]
	public async Task<McpDiscMenuResult> InspectDiscMenusAsync(
		[Description("Root label from list_roots.")] string root,
		[Description("Root-relative path of the disc folder (discType DVD or BluRay).")] string path,
		[Description("Ignore the cached result and inspect again. Rate-limited.")] bool forceRefresh = false,
		CancellationToken ct = default)
	{
		if (forceRefresh && !this._limiter.TryAcquire(this._caller.CallerId, out _))
		{
			throw new McpException("Forced refresh limit reached. Call inspect_disc_menus without forceRefresh to use the cached result.");
		}

		var status = await McpToolHelpers.GuardAsync(() => this._discMenuService.StartOrGetAsync(
			root, McpToolHelpers.NormalizePath(path), forceRefresh, TimeSpan.FromSeconds(Math.Max(0, this._options.ScanWaitSeconds)), ct));
		return ToResult(status);
	}

	[McpServerTool(Name = "get_disc_menu_status", ReadOnly = true, Idempotent = true)]
	[Description("Returns the status of a disc menu inspection started with inspect_disc_menus. Same result shape.")]
	public async Task<McpDiscMenuResult> GetDiscMenuStatusAsync(
		[Description("Root label from list_roots.")] string root,
		[Description("Root-relative path of the disc folder.")] string path,
		CancellationToken ct = default)
	{
		var status = await McpToolHelpers.GuardAsync(() => this._discMenuService.GetStatusAsync(root, McpToolHelpers.NormalizePath(path), ct))
			?? throw new McpException("This disc has not been inspected. Call inspect_disc_menus first.");
		return ToResult(status);
	}

	[McpServerTool(Name = "get_disc_menu_screenshot", ReadOnly = true, Idempotent = true)]
	[Description("Returns a screenshot of one disc menu (from inspect_disc_menus) as an image. With annotated=true every button is outlined and numbered with its button number, so you can read each button's label and match it to its action.")]
	public async Task<CallToolResult> GetDiscMenuScreenshotAsync(
		[Description("Root label from list_roots.")] string root,
		[Description("Root-relative path of the disc folder.")] string path,
		[Description("Menu id from the menu map, e.g. \"m0\".")] string menuId,
		[Description("Draw numbered button outlines on the image.")] bool annotated = true,
		CancellationToken ct = default)
	{
		var screenshot = await McpToolHelpers.GuardAsync(() => this._discMenuService.GetScreenshotAsync(root, McpToolHelpers.NormalizePath(path), menuId, annotated, ct));
		if (screenshot is not { } shot)
		{
			throw new McpException($"No screenshot is available for menu \"{menuId}\". Check the menu's screenshot.error in the menu map.");
		}

		return new CallToolResult
		{
			Content =
			[
				ImageContentBlock.FromBytes(shot.Png, "image/png"),
				new TextContentBlock { Text = Describe(shot.Menu) },
			],
		};
	}

	private static McpDiscMenuResult ToResult(DiscMenuJobStatus status) => status.State switch
	{
		DiscMenuJobState.Inspecting => new McpDiscMenuResult { Status = "Inspecting", StartedAt = status.StartedAt, PollAfterSeconds = ScanTools.PollAfterSeconds },
		DiscMenuJobState.Completed => new McpDiscMenuResult { Status = "Completed", StartedAt = status.StartedAt, MenuMap = status.Map },
		_ => new McpDiscMenuResult { Status = status.State.ToString(), StartedAt = status.StartedAt, Error = status.Error },
	};

	private static string Describe(MenuNode menu)
	{
		var text = new StringBuilder($"Menu {menu.Id} ({menu.Kind}, {menu.Domain}). Buttons:");
		foreach (var button in menu.Buttons)
		{
			var a = button.Action;
			var what = a.Type switch
			{
				ButtonActionType.OpenMenu => $"opens menu {a.MenuId}",
				ButtonActionType.PlayTitle => $"plays title {a.HandBrakeTitle?.ToString() ?? "?"}"
					+ (a.CoversWholeTitle == true ? " (whole title)" : $" chapters {a.StartChapter}-{a.EndChapter?.ToString() ?? "end"}")
					+ (a.Then is null ? string.Empty : $", then {a.Then}{(a.ThenMenuId is null ? string.Empty : " " + a.ThenMenuId)}"),
				ButtonActionType.ChangeSetting => $"changes {a.Setting} to {a.Value}",
				_ => $"unknown ({a.Reason})",
			};
			text.Append($"\n#{button.Number}{(button.IsDefault ? " (default)" : string.Empty)}: {what}");
		}

		return text.ToString();
	}
}
