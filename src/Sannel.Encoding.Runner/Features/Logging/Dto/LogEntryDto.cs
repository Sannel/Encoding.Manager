namespace Sannel.Encoding.Runner.Features.Logging.Dto;

public class LogEntryDto
{
	public DateTimeOffset Timestamp { get; set; }
	public string Level { get; set; } = string.Empty;
	public string Category { get; set; } = string.Empty;
	public string Message { get; set; } = string.Empty;
	public string? Exception { get; set; }
}

public class LogIngestRequest
{
	public string Source { get; set; } = string.Empty;
	public List<LogEntryDto> Entries { get; set; } = [];
}
