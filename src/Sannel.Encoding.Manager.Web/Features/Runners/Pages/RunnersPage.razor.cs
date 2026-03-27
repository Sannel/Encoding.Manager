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

	private async Task DeleteRunnerAsync(Guid id)
	{
		await this.RunnerService.DeleteRunnerAsync(id);
		this._runners = await this.RunnerService.GetRunnersAsync();
		this.Snackbar.Add("Runner deleted.", Severity.Success);
	}
}
