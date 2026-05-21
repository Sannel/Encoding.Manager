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

public partial class QueueJellyfinItemDialog : ComponentBase
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public Guid ServerId { get; set; }

	[Parameter]
	public required JellyfinItem Item { get; set; }

	[Inject]
	private IJellyfinServerService ServerService { get; set; } = default!;

	[Inject]
	private IJellyfinClientFactory ClientFactory { get; set; } = default!;

	[Inject]
	private JellyfinServerService ServerServiceImpl { get; set; } = default!;

	[Inject]
	private IJellyfinEncodeService EncodeService { get; set; } = default!;

	[Inject]
	private IPresetService PresetService { get; set; } = default!;

	[Inject]
	private ISnackbar Snackbar { get; set; } = default!;

	private List<JellyfinServer> _destServers = [];
	private List<DestinationRootOption> _destOptions = [];
	private IReadOnlyList<EncodingPreset> _presets = [];
	private Guid _destServerId;
	private DestinationRootOption? _selectedOption;
	private string _presetLabel = string.Empty;
	private bool _isQueueing;
	private bool _isLoadingOptions;
	private int _optionsLoadVersion;

	private bool CanQueue =>
		this._destServerId != Guid.Empty &&
		this._selectedOption is not null &&
		!string.IsNullOrWhiteSpace(this._presetLabel);

	protected override async Task OnInitializedAsync()
	{
		var servers = await this.ServerService.GetAllServersAsync();
		this._destServers = servers.Where(s => s.IsDestination).ToList();
		this._presets = await this.PresetService.GetPresetsAsync();

		if (this._destServers.Count > 0)
		{
			this._destServerId = this._destServers[0].Id;
			await this.LoadOptionsAsync();
		}
	}

	private async Task OnDestServerChangedAsync(Guid serverId)
	{
		this._destServerId = serverId;
		this._selectedOption = null;
		await this.LoadOptionsAsync();
	}

	private async Task LoadOptionsAsync()
	{
		if (this._destServerId == Guid.Empty)
		{
			this._destOptions = [];
			this._selectedOption = null;
			return;
		}

		var version = ++this._optionsLoadVersion;
		this._isLoadingOptions = true;

		List<DestinationRootOption> options;
		try
		{
			var dbRoots = await this.ServerService.GetDestinationRootsAsync(this._destServerId);
			options = dbRoots
				.Select(r => new DestinationRootOption(r.Id, $"{r.Name} — {r.RootPath}", r.RootPath.TrimEnd('/'), r.ServerId, null))
				.ToList();

			var existingPaths = new HashSet<string>(
				dbRoots.Select(r => r.RootPath.TrimEnd('/')),
				StringComparer.OrdinalIgnoreCase);

			try
			{
				var server = this._destServers.FirstOrDefault(s => s.Id == this._destServerId);
				if (server is not null)
				{
					var client = this.ClientFactory.CreateClient(
						server.BaseUrl,
						this.ServerServiceImpl.DecryptApiKey(server.ApiKey));
					var virtualFolders = await client.GetVirtualFoldersAsync();
					foreach (var folder in virtualFolders)
					{
						foreach (var location in folder.Locations)
						{
							if (string.IsNullOrWhiteSpace(location))
							{
								continue;
							}

							var normalizedPath = location.TrimEnd('/');
							if (existingPaths.Add(normalizedPath))
							{
								options.Add(new DestinationRootOption(
									null,
									$"{folder.Name} — {location}",
									normalizedPath,
									this._destServerId,
									folder.Name));
							}
						}
					}
				}
			}
			catch
			{
				// If Jellyfin is unreachable, fall back to DB roots only
			}
		}
		catch
		{
			options = [];
		}

		if (version != this._optionsLoadVersion)
		{
			return;
		}

		this._destOptions = options;
		this._selectedOption = options.Count > 0 ? options[0] : null;
		this._isLoadingOptions = false;
	}

	private async Task<Guid> EnsureRootIdAsync(DestinationRootOption option)
	{
		if (option.RootId.HasValue)
		{
			return option.RootId.Value;
		}

		// Virtual folder path — find or create a DB root for it
		var existing = await this.ServerService.GetDestinationRootsAsync(option.ServerId);
		var match = existing.FirstOrDefault(r =>
			string.Equals(r.RootPath.TrimEnd('/'), option.RootPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
		if (match is not null)
		{
			return match.Id;
		}

		var name = option.LibraryName ?? Path.GetFileName(option.RootPath) ?? option.RootPath;
		var created = await this.ServerService.CreateDestinationRootAsync(new JellyfinDestinationRootDto
		{
			Name = name,
			ServerId = option.ServerId,
			RootPath = option.RootPath,
		});
		return created.Id;
	}

	private async Task QueueAsync()
	{
		if (this._selectedOption is null)
		{
			return;
		}

		this._isQueueing = true;
		try
		{
			var rootId = await this.EnsureRootIdAsync(this._selectedOption);

			var request = new JellyfinEncodeRequest
			{
				ServerId = this.ServerId,
				ItemId = this.Item.Id,
				PresetLabel = this._presetLabel,
				DestServerId = this._destServerId,
				DestRootId = rootId,
			};

			await this.EncodeService.QueueItemAsync(request);
			this.MudDialog.Close(DialogResult.Ok(true));
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
