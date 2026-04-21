using Renci.SshNet;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public class JellyfinSftpService : IJellyfinSftpService
{
	private const string JellyfinAclUser = "jellyfin";
	private readonly JellyfinServerService _serverService;
	private readonly ILogger<JellyfinSftpService> _logger;

	public JellyfinSftpService(JellyfinServerService serverService, ILogger<JellyfinSftpService> logger)
	{
		this._serverService = serverService;
		this._logger = logger;
	}

	public async Task UploadFileAsync(JellyfinServer destServer, string localFilePath, string remoteFilePath, CancellationToken ct = default)
	{
		var host = destServer.SftpHost ?? new Uri(destServer.BaseUrl).Host;
		var port = destServer.SftpPort;
		var username = destServer.SftpUsername ?? throw new InvalidOperationException("SFTP username is required for destination server.");
		var password = this._serverService.DecryptSftpPassword(destServer.SftpPassword)
			?? throw new InvalidOperationException("SFTP password is required for destination server.");

		this._logger.LogInformation("Uploading {LocalPath} to {Host}:{RemotePath}", localFilePath, host, remoteFilePath);

		using var client = new SftpClient(host, port, username, password);
		await Task.Run(() =>
		{
			client.Connect();
			try
			{
				// Ensure remote directory exists
				var remoteDir = Path.GetDirectoryName(remoteFilePath)?.Replace('\\', '/');
				if (!string.IsNullOrEmpty(remoteDir))
				{
					this.EnsureRemoteDirectoryExists(client, remoteDir);
				}

				using var fileStream = File.OpenRead(localFilePath);
				client.UploadFile(fileStream, remoteFilePath, canOverride: true);
			}
			finally
			{
				client.Disconnect();
			}

			this.ApplyJellyfinAcl(host, port, username, password, remoteFilePath);
		}, ct).ConfigureAwait(false);

		this._logger.LogInformation("Upload complete: {RemotePath}", remoteFilePath);
	}

	private void ApplyJellyfinAcl(string host, int port, string username, string password, string remoteFilePath)
	{
		using var sshClient = new SshClient(host, port, username, password);
		sshClient.Connect();
		try
		{
			var normalizedFilePath = NormalizeRemotePath(remoteFilePath).TrimEnd('/');
			if (string.IsNullOrWhiteSpace(normalizedFilePath))
			{
				throw new InvalidOperationException("Remote file path is required for ACL updates.");
			}

			var directoryPaths = BuildDirectoryAclPaths(normalizedFilePath);
			this._logger.LogInformation(
				"Applying setfacl ACLs for jellyfin. Directories: {Directories}; File: {FilePath}",
				directoryPaths.Count == 0 ? "(none)" : string.Join(", ", directoryPaths),
				normalizedFilePath);

			var commandText = BuildJellyfinAclCommand(normalizedFilePath, directoryPaths);
			var command = sshClient.RunCommand(commandText);
			if (command.ExitStatus != 0)
			{
				var output = string.IsNullOrWhiteSpace(command.Error) ? command.Result : command.Error;
				throw new InvalidOperationException(
					$"Failed to apply setfacl permissions for '{remoteFilePath}'. Exit code {command.ExitStatus}. Output: {output}");
			}
		}
		finally
		{
			if (sshClient.IsConnected)
			{
				sshClient.Disconnect();
			}
		}
	}

	private static string BuildJellyfinAclCommand(string normalizedFilePath, IReadOnlyCollection<string> directoryPaths)
	{
		var commandParts = new List<string>();
		foreach (var directoryPath in directoryPaths)
		{
			commandParts.Add($"setfacl -m u:{JellyfinAclUser}:rwx {QuoteForBash(directoryPath)}");
		}

		commandParts.Add($"setfacl -m u:{JellyfinAclUser}:r {QuoteForBash(normalizedFilePath)}");

		return "set -e; " + string.Join("; ", commandParts);
	}

	private static List<string> BuildDirectoryAclPaths(string remoteFilePath)
	{
		var directoryPath = GetRemoteDirectory(remoteFilePath);
		if (string.IsNullOrWhiteSpace(directoryPath))
		{
			return [];
		}

		var segments = directoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
		var directories = new List<string>(segments.Length);
		var current = directoryPath.StartsWith("/", StringComparison.Ordinal) ? "/" : string.Empty;

		foreach (var segment in segments)
		{
			if (string.IsNullOrEmpty(current))
			{
				current = segment;
			}
			else if (current == "/")
			{
				current = "/" + segment;
			}
			else
			{
				current += "/" + segment;
			}

			directories.Add(current);
		}

		return directories;
	}

	private static string NormalizeRemotePath(string remotePath) =>
		remotePath.Replace('\\', '/');

	private static string GetRemoteDirectory(string remoteFilePath)
	{
		var normalized = NormalizeRemotePath(remoteFilePath).TrimEnd('/');
		var separatorIndex = normalized.LastIndexOf('/');
		return separatorIndex <= 0 ? string.Empty : normalized[..separatorIndex];
	}

	private static string QuoteForBash(string value) =>
		"'" + value.Replace("'", "'\"'\"'") + "'";

	private void EnsureRemoteDirectoryExists(SftpClient client, string remotePath)
	{
		var parts = remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
		var current = string.Empty;
		foreach (var part in parts)
		{
			current += "/" + part;
			try
			{
				if (!client.Exists(current))
				{
					client.CreateDirectory(current);
				}
			}
			catch (Exception ex)
			{
				this._logger.LogError(ex, "Failed to create remote directory '{Directory}' (full target: '{RemotePath}').", current, remotePath);
				throw;
			}
		}
	}
}
