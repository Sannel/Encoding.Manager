using ModelContextProtocol;

namespace Sannel.Encoding.Manager.Web.Features.Mcp.Tools;

/// <summary>Formatting and error helpers shared by the MCP tool classes.</summary>
internal static class McpToolHelpers
{
	public static string FormatDuration(TimeSpan duration) =>
		$"{(int)duration.TotalHours}:{duration.Minutes:D2}:{duration.Seconds:D2}";

	/// <summary>Normalizes a root-relative path to forward slashes without leading/trailing separators.</summary>
	public static string NormalizePath(string? path) =>
		(path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');

	/// <summary>
	/// Runs <paramref name="action"/> and converts the filesystem service's validation exceptions into
	/// <see cref="McpException"/>s whose messages are returned to the model.
	/// </summary>
	public static async Task<T> GuardAsync<T>(Func<Task<T>> action)
	{
		try
		{
			return await action();
		}
		catch (ArgumentException ex)
		{
			throw new McpException($"Invalid root or path: {ex.Message}");
		}
		catch (DirectoryNotFoundException ex)
		{
			throw new McpException($"Directory not found: {ex.Message}");
		}
		catch (FileNotFoundException ex)
		{
			throw new McpException($"File not found: {ex.Message}");
		}
	}

	public static T Guard<T>(Func<T> action)
	{
		try
		{
			return action();
		}
		catch (ArgumentException ex)
		{
			throw new McpException($"Invalid root or path: {ex.Message}");
		}
		catch (DirectoryNotFoundException ex)
		{
			throw new McpException($"Directory not found: {ex.Message}");
		}
	}
}
