using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Hubs;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;
using Sannel.Encoding.Manager.Web.Features.Runner.Dto;
using RunnerEntity = Sannel.Encoding.Manager.Web.Features.Runner.Entities.Runner;

namespace Sannel.Encoding.Manager.Web.Features.Runner.Services;

/// <inheritdoc />
public class RunnerJobService : IRunnerJobService
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;
	private readonly ILogger<RunnerJobService> _logger;
	private readonly IHubContext<QueueHub> _hubContext;
	private readonly QueueChangeNotifier _notifier;
	private readonly IDataProtector _protector;

	public RunnerJobService(
		IDbContextFactory<AppDbContext> dbFactory,
		ILogger<RunnerJobService> logger,
		IHubContext<QueueHub> hubContext,
		QueueChangeNotifier notifier,
		IDataProtectionProvider dpProvider)
	{
		_dbFactory = dbFactory;
		_logger = logger;
		_hubContext = hubContext;
		_notifier = notifier;
		_protector = dpProvider.CreateProtector("Jellyfin.Credentials");
	}

	/// <inheritdoc />
	public async Task RegisterOrUpdateRunnerAsync(string name, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

		var runner = await ctx.Runners
			.FirstOrDefaultAsync(r => r.Name == name, ct)
			.ConfigureAwait(false);

		if (runner is null)
		{
			ctx.Runners.Add(new RunnerEntity
			{
				Name = name,
				LastSeenAt = DateTimeOffset.UtcNow
			});
		}
		else
		{
			runner.LastSeenAt = DateTimeOffset.UtcNow;
		}

		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<bool> IsEnabledAsync(string name, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

		var runner = await ctx.Runners
			.AsNoTracking()
			.FirstOrDefaultAsync(r => r.Name == name, ct)
			.ConfigureAwait(false);

		return runner?.IsEnabled ?? false;
	}

	/// <inheritdoc />
	public async Task<bool> IsCancelRequestedAsync(string runnerName, Guid jobId, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

		var runner = await ctx.Runners
			.AsNoTracking()
			.FirstOrDefaultAsync(r => r.Name == runnerName, ct)
			.ConfigureAwait(false);

		if (runner is null || runner.CurrentJobId != jobId)
		{
			return false;
		}

		var item = await ctx.EncodeQueueItems
			.AsNoTracking()
			.FirstOrDefaultAsync(i => i.Id == jobId && i.RunnerName == runnerName, ct)
			.ConfigureAwait(false);

		if (item is null)
		{
			return false;
		}

		return string.Equals(item.Status, "CancelRequested", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(item.Status, "Canceled", StringComparison.OrdinalIgnoreCase);
	}

	/// <inheritdoc />
	public async Task<ClaimedJobResponse?> ClaimNextJobAsync(string runnerName, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

		// Atomic claim: update the first Queued item to Encoding in a single UPDATE ... WHERE
		var now = DateTimeOffset.UtcNow;
		var nextItem = await ctx.EncodeQueueItems
			.Where(i => i.Status == "Queued")
			.OrderBy(i => i.SortOrder)
			.FirstOrDefaultAsync(ct)
			.ConfigureAwait(false);

		if (nextItem is null)
		{
			return null;
		}

		// Optimistic update: set status to Encoding
		nextItem.Status = "Encoding";
		nextItem.RunnerName = runnerName;
		nextItem.StartedAt = now;

		// Update runner's current job
		var runner = await ctx.Runners
			.FirstOrDefaultAsync(r => r.Name == runnerName, ct)
			.ConfigureAwait(false);
		if (runner is not null)
		{
			runner.CurrentJobId = nextItem.Id;
			runner.LastSeenAt = now;
		}

		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		await NotifyQueueItemUpsertedAsync(nextItem, ct).ConfigureAwait(false);

		// Build preset map from tracks
		var presetMap = await BuildPresetMapAsync(ctx, nextItem.TracksJson, ct).ConfigureAwait(false);

		// Load settings for languages and template
		var settings = await ctx.AppSettings
			.FirstOrDefaultAsync(ct)
			.ConfigureAwait(false)
			?? new Sannel.Encoding.Manager.Web.Features.Settings.Entities.AppSettings();

		var isMovieJob = (nextItem.TvdbId ?? 0) == 0;
		var selectedTemplate = isMovieJob
			? settings.MovieTrackDestinationTemplate
			: settings.TrackDestinationTemplate;

		if (string.IsNullOrWhiteSpace(selectedTemplate))
		{
			selectedTemplate = isMovieJob
				? "Movies/{MovieName} ({MovieYear})/{MovieName} - {Resolution}"
				: "{TVDBShow}/Season {SeasonNumber}/{EpisodeName}";
		}

		return new ClaimedJobResponse
		{
			JobId = nextItem.Id,
			DiscPath = nextItem.DiscPath,
			DiscRootLabel = nextItem.DiscRootLabel,
			Mode = nextItem.Mode,
			TvdbShowName = nextItem.TvdbShowName,
			TvdbId = nextItem.TvdbId,
			AudioDefault = nextItem.AudioDefault,
			TracksJson = nextItem.TracksJson,
			PresetMap = presetMap,
			TrackDestinationTemplate = selectedTemplate,
			TrackDestinationRoot = settings.TrackDestinationRoot,
			AudioLanguages = settings.AudioLanguages
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
			SubtitleLanguages = settings.SubtitleLanguages
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
			JellyfinDownloadUrl = await BuildJellyfinDownloadUrlAsync(ctx, nextItem, ct).ConfigureAwait(false),
			JellyfinApiKey = await GetDecryptedJellyfinApiKeyAsync(ctx, nextItem, ct).ConfigureAwait(false)
		};
	}

	/// <inheritdoc />
	public async Task<bool> UpdateJobStatusAsync(Guid jobId, string status, int? progressPercent, int? currentTrackProgressPercent, string? error, string? encodingCommand = null, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

		var item = await ctx.EncodeQueueItems
			.FirstOrDefaultAsync(i => i.Id == jobId, ct)
			.ConfigureAwait(false);

		if (item is null)
		{
			return false;
		}

		switch (status)
		{
			case "Encoding":
				item.ProgressPercent = progressPercent;
				item.CurrentTrackProgressPercent = currentTrackProgressPercent;
				if (!string.IsNullOrEmpty(encodingCommand))
				{
					AppendEncodingCommand(item, encodingCommand);
				}
				break;

			case "Finished":
				item.Status = "Finished";
				item.CompletedAt = DateTimeOffset.UtcNow;
				item.ProgressPercent = null;
				item.CurrentTrackProgressPercent = null;
				// Clear runner's current job
				await ClearRunnerCurrentJobAsync(ctx, item.RunnerName, ct).ConfigureAwait(false);
				break;

			case "Failed":
				item.Status = "Failed";
				item.CompletedAt = DateTimeOffset.UtcNow;
				item.ProgressPercent = null;
				item.CurrentTrackProgressPercent = null;
				_logger.LogError("Job {JobId} failed: {Error}", jobId, error);
				// Clear runner's current job
				await ClearRunnerCurrentJobAsync(ctx, item.RunnerName, ct).ConfigureAwait(false);
				break;

			case "Canceled":
				item.Status = "Canceled";
				item.CompletedAt = DateTimeOffset.UtcNow;
				item.ProgressPercent = null;
				item.CurrentTrackProgressPercent = null;
				_logger.LogInformation("Job {JobId} canceled.", jobId);
				await ClearRunnerCurrentJobAsync(ctx, item.RunnerName, ct).ConfigureAwait(false);
				break;

			default:
				_logger.LogWarning("Unknown status '{Status}' for job {JobId}", status, jobId);
				return false;
		}

		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		await NotifyQueueItemUpsertedAsync(item, ct).ConfigureAwait(false);
		return true;
	}

	private Task NotifyQueueItemUpsertedAsync(Queue.Entities.EncodeQueueItem item, CancellationToken ct)
	{
		_notifier.NotifyItemUpserted(item);
		return _hubContext.Clients.All.SendAsync("QueueItemUpserted", item, ct);
	}

	private static void AppendEncodingCommand(Queue.Entities.EncodeQueueItem item, string command)
	{
		var commands = !string.IsNullOrEmpty(item.EncodingCommandsJson)
			? JsonSerializer.Deserialize<List<string>>(item.EncodingCommandsJson) ?? []
			: [];
		commands.Add(command);
		item.EncodingCommandsJson = JsonSerializer.Serialize(commands);
	}

	private static async Task ClearRunnerCurrentJobAsync(AppDbContext ctx, string? runnerName, CancellationToken ct)
	{
		if (string.IsNullOrEmpty(runnerName))
		{
			return;
		}

		var runner = await ctx.Runners
			.FirstOrDefaultAsync(r => r.Name == runnerName, ct)
			.ConfigureAwait(false);

		if (runner is not null)
		{
			runner.CurrentJobId = null;
		}
	}

	private static async Task<Dictionary<string, PresetLocation>> BuildPresetMapAsync(
		AppDbContext ctx, string tracksJson, CancellationToken ct)
	{
		var presetMap = new Dictionary<string, PresetLocation>(StringComparer.OrdinalIgnoreCase);

		var tracks = JsonSerializer.Deserialize<List<EncodeTrackConfig>>(tracksJson);
		if (tracks is null)
		{
			return presetMap;
		}

		var presetLabels = tracks
			.Where(t => !string.IsNullOrEmpty(t.PresetLabel))
			.Select(t => t.PresetLabel!)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();

		if (presetLabels.Count == 0)
		{
			return presetMap;
		}

		var presets = await ctx.EncodingPresets
			.AsNoTracking()
			.Where(p => presetLabels.Contains(p.Label))
			.ToListAsync(ct)
			.ConfigureAwait(false);

		foreach (var preset in presets)
		{
			presetMap[preset.Label] = new PresetLocation
			{
				PresetName = preset.PresetName,
				RootLabel = preset.RootLabel,
				RelativePath = preset.RelativePath
			};
		}

		return presetMap;
	}

	private async Task<string?> BuildJellyfinDownloadUrlAsync(
		AppDbContext ctx, Queue.Entities.EncodeQueueItem item, CancellationToken ct)
	{
		if (item.JellyfinSourceServerId is null || string.IsNullOrEmpty(item.JellyfinSourceItemId))
		{
			return null;
		}

		var server = await ctx.JellyfinServers
			.AsNoTracking()
			.FirstOrDefaultAsync(s => s.Id == item.JellyfinSourceServerId, ct)
			.ConfigureAwait(false);

		if (server is null)
		{
			return null;
		}

		return $"{server.BaseUrl.TrimEnd('/')}/Items/{item.JellyfinSourceItemId}/Download";
	}

	private async Task<string?> GetDecryptedJellyfinApiKeyAsync(
		AppDbContext ctx, Queue.Entities.EncodeQueueItem item, CancellationToken ct)
	{
		if (item.JellyfinSourceServerId is null)
		{
			return null;
		}

		var server = await ctx.JellyfinServers
			.AsNoTracking()
			.FirstOrDefaultAsync(s => s.Id == item.JellyfinSourceServerId, ct)
			.ConfigureAwait(false);

		if (server is null)
		{
			return null;
		}

		return _protector.Unprotect(server.ApiKey);
	}
}
