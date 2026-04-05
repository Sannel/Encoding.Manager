namespace Sannel.Encoding.Runner.Features.Runner.Services;

public interface IRunnerAccessTokenProvider
{
	Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}
