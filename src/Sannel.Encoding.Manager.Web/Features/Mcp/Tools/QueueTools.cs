using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Sannel.Encoding.Manager.Web.Features.Mcp.Dto;
using Sannel.Encoding.Manager.Web.Features.Mcp.Services;
using Sannel.Encoding.Manager.Web.Features.Queue.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Services;
using Sannel.Encoding.Manager.Web.Features.Scan.Services;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Tools;

/// <summary>MCP tools that queue encodes and read the queue.</summary>
[McpServerToolType]
public class QueueTools
{
	private readonly McpQueueRequestBuilder _builder;
	private readonly IEncodeJobSubmissionService _submissionService;
	private readonly IEncodeQueueService _queueService;
	private readonly McpCaller _caller;

	public QueueTools(
		McpQueueRequestBuilder builder,
		IEncodeJobSubmissionService submissionService,
		IEncodeQueueService queueService,
		McpCaller caller)
	{
		this._builder = builder;
		this._submissionService = submissionService;
		this._queueService = queueService;
		this._caller = caller;
	}

	[McpServerTool(Name = "queue_encode_job", Destructive = false)]
	[Description("""
		Adds one encode job to the queue, exactly like the Scan page's "Add to Queue".
		selection "disc": path = disc folder; mode "Titles" (one track per titleNumber) or "Chapters" (each track = titleNumber + startChapter..endChapter). The disc must have been scanned with scan_disc.
		selection "folder": path = media folder; each track's sourceRelativePath comes from list_folder_media_files.
		selection "file": path = the media file; exactly one track.
		TV: set tvdbSeriesId and each track's seasonNumber/episodeNumber, outputName = episode name. Movies: set movieYear and each track's resolution, outputName = movie title.
		Tracks with a blank outputName are skipped. All validation problems are reported at once and nothing is queued until the request is valid.
		""")]
	public async Task<McpQueueEncodeResult> QueueEncodeJobAsync(
		[Description("The job to queue.")] McpQueueEncodeRequest request,
		CancellationToken ct)
	{
		var (submission, errors) = await this._builder.BuildAsync(request, this._caller.DisplayName, this._caller.ObjectId, ct);
		if (submission is null)
		{
			throw new McpException("The job was not queued:\n- " + string.Join("\n- ", errors));
		}

		var result = await this._submissionService.SubmitAsync(submission, ct);
		if (!result.Accepted || result.QueueItemId is not { } id)
		{
			throw new McpException(result.RejectionReason ?? "The job was not queued.");
		}

		return new McpQueueEncodeResult
		{
			QueueItemId = id,
			TrackCount = result.TrackCount,
			SkippedTracks = result.SkippedTrackCount,
		};
	}

	[McpServerTool(Name = "get_queue", ReadOnly = true, Idempotent = true)]
	[Description("Lists encode queue items in processing order with their status and progress.")]
	public async Task<IReadOnlyList<McpQueueItemSummary>> GetQueueAsync(
		[Description("Include items that were cleared from the queue view.")] bool includeArchived = false,
		CancellationToken ct = default)
	{
		var items = await this._queueService.GetItemsAsync(includeArchived, ct);
		return items.Select(i => new McpQueueItemSummary
		{
			Id = i.Id,
			RootLabel = i.DiscRootLabel,
			DiscPath = i.DiscPath,
			Mode = i.Mode,
			Status = i.Status,
			ProgressPercent = i.ProgressPercent,
			TrackCount = CountTracks(i.TracksJson),
			TvdbShowName = i.TvdbShowName,
			CreatedAt = i.CreatedAt,
			CreatedBy = i.CreatedBy,
			CreatedVia = i.CreatedVia,
		}).ToList();
	}

	private static int CountTracks(string tracksJson)
	{
		try
		{
			return JsonSerializer.Deserialize<List<EncodeTrackConfig>>(tracksJson)?.Count ?? 0;
		}
		catch (JsonException)
		{
			return 0;
		}
	}
}
