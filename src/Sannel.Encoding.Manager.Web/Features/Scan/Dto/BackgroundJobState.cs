namespace Sannel.Encoding.Manager.Web.Features.Scan.Dto;

/// <summary>State of a long-running background job (disc scan, menu inspection).</summary>
public enum BackgroundJobState
{
	Running,
	Completed,
	Failed,
}
