namespace Sannel.Encoding.Runner.Features.Runner.Options;

/// <summary>Configuration for the encoding runner service.</summary>
public class RunnerOptions
{
	/// <summary>Unique name of this runner instance (e.g. "runner-01").</summary>
	public string Name { get; set; } = "runner-01";

	/// <summary>Base URL of the web app API (e.g. "https://encoding.example.com").</summary>
	public string ServiceBaseUrl { get; set; } = string.Empty;

	/// <summary>Seconds between poll cycles.</summary>
	public int PollIntervalSeconds { get; set; } = 60;
}
