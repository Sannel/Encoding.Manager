using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Jellyfin;
using Sannel.Encoding.Manager.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Components;

public partial class QueueBulkDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public Guid ServerId { get; set; }

	[Parameter]
	public required IJellyfinClient Client { get; set; }

	[Parameter]
	public required JellyfinItem ParentItem { get; set; }

	[Inject]
	private IJellyfinServerService ServerService { get; set; } = default!;

	[Inject]
	private IJellyfinEncodeService EncodeService { get; set; } = default!;

	[Inject]
	private IPresetService PresetService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	private List<JellyfinItem> _episodes = [];
	private List<JellyfinServer> _destServers = [];
	private List<JellyfinDestinationRoot> _destRoots = [];
	private IReadOnlyList<EncodingPreset> _presets = [];
	private Guid _destServerId;
	private Guid _destRootId;
	private string _presetLabel = string.Empty;
	private bool _isLoadingEpisodes = true;
	private bool _isQueueing;

	private string _itemTypeName =>
		string.Equals(this.ParentItem.Type, "Series", StringComparison.OrdinalIgnoreCase)
			? $"Series: {this.ParentItem.Name}"
			: $"Season: {this.ParentItem.Name}";

	private bool CanQueue =>
		this._episodes.Count > 0 &&
		this._destServerId != Guid.Empty &&
		this._destRootId != Guid.Empty &&
		!string.IsNullOrWhiteSpace(this._presetLabel);

	protected override async Task OnInitializedAsync()
	{
		var loadEpisodesTask = this.LoadEpisodesAsync();
		var loadServersTask = this.LoadDestinationsAsync();

		await Task.WhenAll(loadEpisodesTask, loadServersTask);
	}

	private async Task LoadEpisodesAsync()
	{
		this._isLoadingEpisodes = true;
		try
		{
			var episodes = new List<JellyfinItem>();
			var startIndex = 0;
			const int pageSize = 500;

			while (true)
			{
				var response = await this.Client.GetItemsAsync(new GetItemsRequest
				{
					ParentId = this.ParentItem.Id,
					IncludeItemTypes = "Episode",
					Recursive = true,
					Fields = "ProviderIds",
					StartIndex = startIndex,
					Limit = pageSize,
				});

				episodes.AddRange(response.Items);

				if (episodes.Count >= response.TotalRecordCount)
				{
					break;
				}

				startIndex += pageSize;
			}

			this._episodes = episodes
				.OrderBy(e => e.ParentIndexNumber ?? 0)
				.ThenBy(e => e.IndexNumber ?? 0)
				.ToList();
		}
		catch (Exception ex)
		{
			this.Snackbar.Add($"Failed to load episodes: {ex.Message}", Severity.Error);
			this._episodes = [];
		}
		finally
		{
			this._isLoadingEpisodes = false;
		}
	}

	private async Task LoadDestinationsAsync()
	{
		var servers = await this.ServerService.GetAllServersAsync();
		this._destServers = servers.Where(s => s.IsDestination).ToList();
		this._presets = await this.PresetService.GetPresetsAsync();

		if (this._destServers.Count > 0)
		{
			this._destServerId = this._destServers[0].Id;
			await this.LoadRootsAsync();
		}
	}

	private async Task OnDestServerChangedAsync(Guid serverId)
	{
		this._destServerId = serverId;
		await this.LoadRootsAsync();
	}

	private async Task LoadRootsAsync()
	{
		this._destRoots = (await this.ServerService.GetDestinationRootsAsync(this._destServerId)).ToList();
		this._destRootId = this._destRoots.Count > 0 ? this._destRoots[0].Id : Guid.Empty;
	}

	private async Task QueueAllAsync()
	{
		this._isQueueing = true;
		var queued = 0;
		var failed = 0;

		try
		{
			foreach (var episode in this._episodes)
			{
				try
				{
					var request = new JellyfinEncodeRequest
					{
						ServerId = this.ServerId,
						ItemId = episode.Id,
						PresetLabel = this._presetLabel,
						DestServerId = this._destServerId,
						DestRootId = this._destRootId,
					};

					await this.EncodeService.QueueItemAsync(request);
					queued++;
				}
				catch
				{
					failed++;
				}
			}

			if (failed == 0)
			{
				this.Snackbar.Add($"Queued {queued} episode(s) for encoding.", Severity.Success);
			}
			else
			{
				this.Snackbar.Add($"Queued {queued}, failed {failed} episode(s).", Severity.Warning);
			}

			this.MudDialog.Close(DialogResult.Ok(queued));
		}
		catch (Exception ex)
		{
			this.Snackbar.Add($"Failed to queue: {ex.Message}", Severity.Error);
		}
		finally
		{
			this._isQueueing = false;
		}
	}

	private void Cancel() => this.MudDialog.Cancel();
}
