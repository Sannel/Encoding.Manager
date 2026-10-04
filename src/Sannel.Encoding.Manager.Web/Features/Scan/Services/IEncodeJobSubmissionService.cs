using Sannel.Encoding.Manager.Web.Features.Scan.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Services;

/// <summary>
/// Builds and persists a disk-level encode queue item. Used by the Scan page and the MCP server.
/// </summary>
public interface IEncodeJobSubmissionService
{
	/// <summary>
	/// Drops tracks with a blank output name, stamps the preset and the global audio default,
	/// and adds the item to the queue. Rejects the submission when no named tracks remain.
	/// </summary>
	Task<EncodeJobSubmissionResult> SubmitAsync(EncodeJobSubmission submission, CancellationToken ct = default);
}
