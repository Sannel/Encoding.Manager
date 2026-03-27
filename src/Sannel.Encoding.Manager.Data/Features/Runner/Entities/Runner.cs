namespace Sannel.Encoding.Manager.Web.Features.Runner.Entities;

/// <summary>
/// Represents a registered encoding runner instance.
/// </summary>
public class Runner
{
	public Guid Id { get; set; } = Guid.NewGuid();

	/// <summary>Unique runner name (e.g. "runner-01").</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>Whether this runner is allowed to claim jobs.</summary>
	public bool IsEnabled { get; set; } = true;

	/// <summary>Last time the runner sent a heartbeat.</summary>
	public DateTimeOffset? LastSeenAt { get; set; }

	/// <summary>Informational: the queue item this runner is currently working on.</summary>
	public Guid? CurrentJobId { get; set; }
}
