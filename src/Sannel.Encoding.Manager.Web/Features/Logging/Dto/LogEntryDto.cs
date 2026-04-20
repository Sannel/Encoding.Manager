namespace Sannel.Encoding.Manager.Web.Features.Logging.Dto;

public class LogEntryDto
{
	public DateTimeOffset Timestamp { get; set; }
	public string Level { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public string? Exception { get; set; }
}
