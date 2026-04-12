using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Runners.Dto;
using Sannel.Encoding.Manager.Web.Features.Runners.Services;

namespace Sannel.Encoding.Manager.Web.Features.Runners.Pages;

public partial class RunnersPage : ComponentBase
{
	[Inject]
	private IRunnerManagementService RunnerService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	private IReadOnlyList<RunnerDto> _runners = [];
	private bool _isLoading = true;

	protected override async Task OnInitializedAsync()
	{
		this._runners = await this.RunnerService.GetRunnersAsync();
		this._isLoading = false;
	}

	private async Task ToggleEnabledAsync(Guid id, bool enabled)
	{
		await this.RunnerService.SetEnabledAsync(id, enabled);
		this._runners = await this.RunnerService.GetRunnersAsync();
	}

	private async Task ResetRunnerAsync(Guid id)
	{
		var reset = await this.RunnerService.ResetRunnerAsync(id);
		this._runners = await this.RunnerService.GetRunnersAsync();

		if (reset)
		{
			this.Snackbar.Add("Runner reset: disabled and active encode cancellation requested.", Severity.Info);
		}
		else
		{
			this.Snackbar.Add("Runner not found.", Severity.Warning);
		}
	}

	private async Task DeleteRunnerAsync(Guid id)
	{
		await this.RunnerService.DeleteRunnerAsync(id);
		this._runners = await this.RunnerService.GetRunnersAsync();
		this.Snackbar.Add("Runner deleted.", Severity.Success);
	}

	private static string DiscLabel(string discPath) =>
		Path.GetFileName(discPath.TrimEnd(Path.DirectorySeparatorChar, '/'))
		?? discPath;

	private static bool ShouldShowCurrentJobBadge(string? status) =>
		string.Equals(status, "CancelRequested", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(status, "Encoding", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase);

	private static Color GetCurrentJobStatusColor(string? status) => status switch
	{
		"CancelRequested" => Color.Info,
		"Encoding" => Color.Warning,
		"Canceled" => Color.Secondary,
		_ => Color.Default,
	};
}
