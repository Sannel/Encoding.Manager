using System.Diagnostics;
using System.Text;

namespace Sannel.Encoding.Manager.HandBrake;

/// <summary>
/// Default implementation of <see cref="IProcessRunner"/> using System.Diagnostics.Process.
/// </summary>
public class ProcessRunner : IProcessRunner
{
	public async Task<ProcessResult> RunAsync(
		string fileName,
		IEnumerable<string> arguments,
		CancellationToken ct = default)
	{
		using var process = CreateProcess(fileName, arguments);
		process.Start();

		var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
		var stderrTask = process.StandardError.ReadToEndAsync(ct);

		await process.WaitForExitAsync(ct).ConfigureAwait(false);

		return new ProcessResult
		{
			ExitCode = process.ExitCode,
			StandardOutput = await stdoutTask.ConfigureAwait(false),
			StandardError = await stderrTask.ConfigureAwait(false)
		};
	}

	public async Task<ProcessResult> RunAsync(
		string fileName,
		IEnumerable<string> arguments,
		IReadOnlyDictionary<string, string?> environment,
		CancellationToken ct = default)
	{
		using var process = CreateProcess(fileName, arguments);
		foreach (var (key, value) in environment)
		{
			if (value is null)
			{
				process.StartInfo.Environment.Remove(key);
			}
			else
			{
				process.StartInfo.Environment[key] = value;
			}
		}

		process.Start();

		// Read with CancellationToken.None so the pipes drain even after a kill.
		var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
		var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

		try
		{
			await process.WaitForExitAsync(ct).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			try
			{
				process.Kill(entireProcessTree: true);
			}
			catch (InvalidOperationException)
			{
				// Already exited.
			}

			throw;
		}

		return new ProcessResult
		{
			ExitCode = process.ExitCode,
			StandardOutput = await stdoutTask.ConfigureAwait(false),
			StandardError = await stderrTask.ConfigureAwait(false)
		};
	}

	public async Task<ProcessResult> RunWithLineCallbackAsync(
		string fileName,
		IEnumerable<string> arguments,
		Action<string> onOutputLine,
		CancellationToken ct = default)
	{
		using var process = CreateProcess(fileName, arguments);
		var stderr = new StringBuilder();

		process.Start();
		process.BeginErrorReadLine();
		process.ErrorDataReceived += (_, e) =>
		{
			if (e.Data is not null)
			{
				stderr.AppendLine(e.Data);
			}
		};

		var stdout = new StringBuilder();
		while (await process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
		{
			stdout.AppendLine(line);
			onOutputLine(line);
		}

		await process.WaitForExitAsync(ct).ConfigureAwait(false);

		return new ProcessResult
		{
			ExitCode = process.ExitCode,
			StandardOutput = stdout.ToString(),
			StandardError = stderr.ToString()
		};
	}

	private static Process CreateProcess(string fileName, IEnumerable<string> arguments)
	{
		var psi = new ProcessStartInfo(fileName)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		foreach (var arg in arguments)
		{
			psi.ArgumentList.Add(arg);
		}

		return new Process { StartInfo = psi };
	}
}
