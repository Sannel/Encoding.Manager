namespace Sannel.Encoding.Manager.Web.Features.Logging.Entities;

public class LogEntry
{
	public long Id { get; set; }
	public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
	public string Level { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public string? Exception { get; set; }
	public string? Source { get; set; }
}
