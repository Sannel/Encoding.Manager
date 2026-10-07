using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Components;

/// <summary>Shows a freshly generated API key once, with a copy button.</summary>
public partial class NewKeyRevealCard : ComponentBase
{
	[Inject]
	private IJSRuntime JS { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	[Parameter]
	[EditorRequired]
	public string Key { get; set; } = string.Empty;

	private async Task CopyAsync()
	{
		await this.JS.InvokeVoidAsync("navigator.clipboard.writeText", this.Key);
		this.Snackbar.Add("API key copied.", Severity.Success);
	}
}
