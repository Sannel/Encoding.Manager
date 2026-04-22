using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Components;

public partial class SyncDebugDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public JellyfinSyncProfile? Profile { get; set; }

	[Inject]
	private IJellyfinSyncService SyncService { get; set; } = default!;

	private List<SyncCompareRow> _rows = [];
	private string? _error;
	private bool _isLoading = true;
	private string _search = string.Empty;

	private List<SyncCompareRow> Filtered
	{
		get
		{
			var rows = this._rows
				.Where(r => r.UserDataA is not null && r.UserDataB is not null
					&& r.UserDataA.LastPlayedDate != r.UserDataB.LastPlayedDate);

			if (!string.IsNullOrWhiteSpace(this._search))
			{
				rows = rows.Where(r => r.DisplayName.Contains(this._search, StringComparison.OrdinalIgnoreCase));
			}

			return rows.ToList();
		}
	}

	protected override async Task OnInitializedAsync()
	{
		if (this.Profile is null)
		{
			this._error = "No profile provided.";
			this._isLoading = false;
			return;
		}

		try
		{
			var result = await this.SyncService.GetComparisonAsync(this.Profile);
			this._rows = result.ToList();
		}
		catch (Exception ex)
		{
			this._error = ex.Message;
		}
		finally
		{
			this._isLoading = false;
		}
	}

	private static string FormatDate(DateTimeOffset? date) =>
		date.HasValue ? date.Value.ToLocalTime().ToString("g") : "—";
}
