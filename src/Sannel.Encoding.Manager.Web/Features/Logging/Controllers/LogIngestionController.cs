using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Logging.Dto;
using Sannel.Encoding.Manager.Web.Features.Logging.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Logging.Controllers;

[ApiController]
[Route("api/logs")]
[Authorize(Policy = "RunnerApi")]
public class LogIngestionController : ControllerBase
{
	private readonly IDbContextFactory<AppDbContext> _contextFactory;
	private readonly ILogger<LogIngestionController> _logger;

	public LogIngestionController(IDbContextFactory<AppDbContext> contextFactory, ILogger<LogIngestionController> logger) =>
		(this._contextFactory, this._logger) = (contextFactory, logger);

	[HttpPost("ingest")]
	public async Task<IActionResult> IngestAsync([FromBody] LogIngestRequest request)
	{
		if (request.Entries is null || request.Entries.Count == 0)
		{
			return this.BadRequest("No log entries provided.");
		}

		if (string.IsNullOrWhiteSpace(request.Source))
		{
			return this.BadRequest("Source is required.");
		}

		const int maxBatchSize = 1000;
		if (request.Entries.Count > maxBatchSize)
		{
			return this.BadRequest($"Batch size exceeds maximum of {maxBatchSize}.");
		}

		var entities = request.Entries.Select(e => new LogEntry
		{
			Timestamp = e.Timestamp,
			Level = e.Level,
			Category = e.Category,
			Message = e.Message,
			Exception = e.Exception,
			Source = request.Source,
		});

		await using var context = await this._contextFactory.CreateDbContextAsync();
		context.LogEntries.AddRange(entities);
		await context.SaveChangesAsync();

		this._logger.LogDebug("Ingested {Count} log entries from {Source}.", request.Entries.Count, request.Source);

		return this.Ok(new { Ingested = request.Entries.Count });
	}
}
