using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Dto;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Services;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Components;

public partial class TvdbSearchDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Inject]
	private ITvdbService TvdbService { get; set; } = default!;

	private string _searchTerm = string.Empty;
	private bool _isSearching;
	private bool _searched;
	private string? _errorMessage;
	private IReadOnlyList<TvdbSeriesSearchResult> _results = [];

	private async Task SearchAsync()
	{
		if (string.IsNullOrWhiteSpace(this._searchTerm) || this._isSearching)
		{
			return;
		}

		this._isSearching = true;
		this._errorMessage = null;
		this._results = [];
		this._searched = false;

		try
		{
			this._results = await this.TvdbService.SearchSeriesAsync(this._searchTerm.Trim());
			this._searched = true;
		}
		catch (Exception ex)
		{
			this._errorMessage = $"Search failed: {ex.Message}";
		}
		finally
		{
			this._isSearching = false;
		}
	}

	private async Task OnKeyDown(KeyboardEventArgs e)
	{
		if (e.Key == "Enter")
		{
			await this.SearchAsync();
		}
	}

	private void SelectResult(TvdbSeriesSearchResult result) =>
		this.MudDialog.Close(DialogResult.Ok(result));

	private void Cancel() => this.MudDialog.Cancel();
}
