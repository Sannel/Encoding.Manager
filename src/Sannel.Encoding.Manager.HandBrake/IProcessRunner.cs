namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>
/// Abstraction over System.Diagnostics.Process to allow unit testing without spawning real processes.
/// </summary>
public interface IProcessRunner
{
	/// <summary>
	/// Runs a process to completion, capturing all stdout and stderr.
	/// </summary>
	Task<ProcessResult> RunAsync(
		string fileName,
		IEnumerable<string> arguments,
		CancellationToken ct = default);

	/// <summary>
	/// Runs a process to completion with extra environment variables, capturing all stdout and stderr.
	/// When <paramref name="ct"/> is cancelled the whole process tree is killed before the exception propagates.
	/// </summary>
	/// <param name="environment">Variables to set (non-null value) or remove (null value) for the child process.</param>
	Task<ProcessResult> RunAsync(
		string fileName,
		IEnumerable<string> arguments,
		IReadOnlyDictionary<string, string?> environment,
		CancellationToken ct = default);

	/// <summary>
	/// Runs a process and streams stdout lines as they arrive.
	/// Stderr is captured fully and returned in the result.
	/// </summary>
	Task<ProcessResult> RunWithLineCallbackAsync(
		string fileName,
		IEnumerable<string> arguments,
		Action<string> onOutputLine,
		CancellationToken ct = default);
}
