using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Runner.Dto;
using RunnerEntity = Sannel.Encoding.Manager.Web.Features.Runner.Entities.Runner;

namespace Sannel.Encoding.Manager.Web.Features.Runner.Services;

/// <inheritdoc />
public class RunnerJobService : IRunnerJobService
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;
	private readonly ILogger<RunnerJobService> _logger;

	public RunnerJobService(IDbContextFactory<AppDbContext> dbFactory, ILogger<RunnerJobService> logger)
	{
		_dbFactory = dbFactory;
		_logger = logger;
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
	public async Task<ClaimedJobResponse?> ClaimNextJobAsync(string runnerName, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

		// Atomic claim: update the first Queued item to Encoding in a single UPDATE ... WHERE
		var now = DateTimeOffset.UtcNow;
		var nextItem = await ctx.EncodeQueueItems
			.Where(i => i.Status == "Queued")
			.OrderBy(i => i.CreatedAt)
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

		// Build preset map from tracks
		var presetMap = await BuildPresetMapAsync(ctx, nextItem.TracksJson, ct).ConfigureAwait(false);

		// Load settings for languages and template
		var settings = await ctx.AppSettings
			.FirstOrDefaultAsync(ct)
			.ConfigureAwait(false)
			?? new Sannel.Encoding.Manager.Web.Features.Settings.Entities.AppSettings();

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
			TrackDestinationTemplate = settings.TrackDestinationTemplate,
			TrackDestinationRoot = settings.TrackDestinationRoot,
			AudioLanguages = settings.AudioLanguages
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
			SubtitleLanguages = settings.SubtitleLanguages
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
		};
	}

	/// <inheritdoc />
	public async Task<bool> UpdateJobStatusAsync(Guid jobId, string status, int? progressPercent, string? error, CancellationToken ct = default)
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
				break;

			case "Finished":
				item.Status = "Finished";
				item.CompletedAt = DateTimeOffset.UtcNow;
				item.ProgressPercent = null;
				// Clear runner's current job
				await ClearRunnerCurrentJobAsync(ctx, item.RunnerName, ct).ConfigureAwait(false);
				break;

			case "Failed":
				item.Status = "Failed";
				item.CompletedAt = DateTimeOffset.UtcNow;
				item.ProgressPercent = null;
				_logger.LogError("Job {JobId} failed: {Error}", jobId, error);
				// Clear runner's current job
				await ClearRunnerCurrentJobAsync(ctx, item.RunnerName, ct).ConfigureAwait(false);
				break;

			default:
				_logger.LogWarning("Unknown status '{Status}' for job {JobId}", status, jobId);
				return false;
		}

		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		return true;
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
				RootLabel = preset.RootLabel,
				RelativePath = preset.RelativePath
			};
		}

		return presetMap;
	}
}
