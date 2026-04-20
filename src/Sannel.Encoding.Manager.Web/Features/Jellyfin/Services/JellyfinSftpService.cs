using Renci.SshNet;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public class JellyfinSftpService : IJellyfinSftpService
{
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
		}, ct).ConfigureAwait(false);

		this._logger.LogInformation("Upload complete: {RemotePath}", remoteFilePath);
	}

	private void EnsureRemoteDirectoryExists(SftpClient client, string remotePath)
	{
		var parts = remotePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
		var current = string.Empty;
		foreach (var part in parts)
		{
			current += "/" + part;
			if (!client.Exists(current))
			{
				client.CreateDirectory(current);
			}
		}
	}
}
