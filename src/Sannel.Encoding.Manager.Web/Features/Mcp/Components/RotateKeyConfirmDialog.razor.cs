using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Components;

/// <summary>Confirms rotating or revoking an MCP API key.</summary>
public partial class RotateKeyConfirmDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public string Title { get; set; } = "Rotate API key?";

	[Parameter]
	public string Message { get; set; } = "A new key will be generated and the current key will stop working.";

	[Parameter]
	public string ConfirmText { get; set; } = "Rotate";

	[Parameter]
	public Color ConfirmColor { get; set; } = Color.Warning;

	private void Confirm() => this.MudDialog.Close(DialogResult.Ok(true));

	private void Cancel() => this.MudDialog.Cancel();
}
