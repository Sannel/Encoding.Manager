namespace Sannel.Encoding.Manager.Web.Features.Logging.Dto;

public class LogIngestRequest
{
	public string Source { get; set; } = string.Empty;
	public List<LogEntryDto> Entries { get; set; } = [];
}
