using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Logging.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Logging.Components;

public partial class LogDetailDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public required LogEntry Entry { get; set; }

	private void Close() => this.MudDialog.Close();
}
