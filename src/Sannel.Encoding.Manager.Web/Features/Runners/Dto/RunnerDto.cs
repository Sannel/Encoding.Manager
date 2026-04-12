namespace Sannel.Encoding.Manager.Web.Features.Runners.Dto;

/// <summary>View model for the Runners management page.</summary>
public class RunnerDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public bool IsEnabled { get; set; }
	public DateTimeOffset? LastSeenAt { get; set; }
	public Guid? CurrentJobId { get; set; }
	public string? CurrentJobName { get; set; }
	public string? CurrentJobStatus { get; set; }
}
