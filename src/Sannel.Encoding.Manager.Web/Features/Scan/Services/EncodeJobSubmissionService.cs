using System.Text.Json;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;
using Sannel.Encoding.Manager.Web.Features.Scan.Dto;
using Sannel.Encoding.Manager.Web.Features.Settings.Services;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Services;

/// <inheritdoc />
public class EncodeJobSubmissionService : IEncodeJobSubmissionService
{
	private readonly IEncodeQueueService _queueService;
	private readonly ISettingsService _settingsService;

	public EncodeJobSubmissionService(IEncodeQueueService queueService, ISettingsService settingsService)
	{
		this._queueService = queueService;
		this._settingsService = settingsService;
	}

	/// <inheritdoc />
	public async Task<EncodeJobSubmissionResult> SubmitAsync(EncodeJobSubmission submission, CancellationToken ct = default)
	{
		var toAdd = submission.Tracks.Where(t => !string.IsNullOrWhiteSpace(t.OutputName)).ToList();
		var skipped = submission.Tracks.Count - toAdd.Count;
		if (toAdd.Count == 0)
		{
			return EncodeJobSubmissionResult.Rejected("No tracks to queue — all track names are empty.", skipped);
		}

		// A track's own preset (per-track choice, e.g. Decomb for an interlaced extra) wins; the rest use the job preset.
		foreach (var track in toAdd)
		{
			track.PresetLabel = string.IsNullOrWhiteSpace(track.PresetLabel) ? submission.PresetLabel : track.PresetLabel;
		}

		var settings = await this._settingsService.GetSettingsAsync(ct);
		var item = new EncodeQueueItem
		{
			DiscPath = submission.DiscPath,
			DiscRootLabel = submission.RootLabel,
			Mode = submission.Mode,
			TvdbShowName = submission.TvdbShowName,
			TvdbId = submission.TvdbId,
			TracksJson = JsonSerializer.Serialize(toAdd),
			AudioDefault = settings.AudioDefault,
			CreatedBy = submission.CreatedBy,
			CreatedByObjectId = submission.CreatedByObjectId,
			CreatedVia = submission.CreatedVia,
		};

		await this._queueService.AddItemAsync(item, ct);
		return new EncodeJobSubmissionResult
		{
			Accepted = true,
			QueueItemId = item.Id,
			TrackCount = toAdd.Count,
			SkippedTrackCount = skipped,
		};
	}
}
