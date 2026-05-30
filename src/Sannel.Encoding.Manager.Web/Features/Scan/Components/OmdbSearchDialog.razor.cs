using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Omdb.Dto;
using Sannel.Encoding.Manager.Web.Features.Omdb.Services;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Components;

public partial class OmdbSearchDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Inject]
	private IOmdbService OmdbService { get; set; } = default!;

	private string _searchTerm = string.Empty;
	private bool _isSearching;
	private bool _searched;
	private string? _errorMessage;
	private IReadOnlyList<OmdbSearchResult> _results = [];

	protected override void OnInitialized()
	{
		if (!this.OmdbService.IsConfigured)
		{
			this._errorMessage = "OMDb is not configured. Set the OMDb API key in application settings.";
		}
	}

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
			this._results = await this.OmdbService.SearchMoviesListAsync(this._searchTerm.Trim());
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

	private void SelectResult(OmdbSearchResult result) =>
		this.MudDialog.Close(DialogResult.Ok(result));

	private void Cancel() => this.MudDialog.Cancel();
}
