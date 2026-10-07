using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public class JellyfinServerService : IJellyfinServerService
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;
	private readonly IDataProtector _protector;

	public JellyfinServerService(IDbContextFactory<AppDbContext> dbFactory, IDataProtectionProvider dpProvider)
	{
		this._dbFactory = dbFactory;
		this._protector = dpProvider.CreateProtector("Jellyfin.Credentials");
	}

	public async Task<IReadOnlyList<JellyfinServer>> GetAllServersAsync(CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await ctx.JellyfinServers.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct).ConfigureAwait(false);
	}

	public async Task<JellyfinServer?> GetServerAsync(Guid id, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await ctx.JellyfinServers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
	}

	public async Task<JellyfinServer> CreateServerAsync(JellyfinServerDto dto, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var server = new JellyfinServer
		{
			Name = dto.Name,
			BaseUrl = dto.BaseUrl,
			ApiKey = this._protector.Protect(dto.ApiKey),
			IsSource = dto.IsSource,
			IsDestination = dto.IsDestination,
			SftpHost = dto.SftpHost,
			SftpPort = dto.SftpPort,
			SftpUsername = dto.SftpUsername,
			SftpPassword = !string.IsNullOrEmpty(dto.SftpPassword) ? this._protector.Protect(dto.SftpPassword) : null,
		};
		ctx.JellyfinServers.Add(server);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		return server;
	}

	public async Task<JellyfinServer?> UpdateServerAsync(Guid id, JellyfinServerDto dto, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var server = await ctx.JellyfinServers.FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
		if (server is null)
		{
			return null;
		}

		server.Name = dto.Name;
		server.BaseUrl = dto.BaseUrl;
		server.ApiKey = this._protector.Protect(dto.ApiKey);
		server.IsSource = dto.IsSource;
		server.IsDestination = dto.IsDestination;
		server.SftpHost = dto.SftpHost;
		server.SftpPort = dto.SftpPort;
		server.SftpUsername = dto.SftpUsername;
		server.SftpPassword = !string.IsNullOrEmpty(dto.SftpPassword) ? this._protector.Protect(dto.SftpPassword) : null;

		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		return server;
	}

	public async Task<bool> DeleteServerAsync(Guid id, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var server = await ctx.JellyfinServers.FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
		if (server is null)
		{
			return false;
		}

		ctx.JellyfinServers.Remove(server);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		return true;
	}

	public async Task<IReadOnlyList<JellyfinDestinationRoot>> GetDestinationRootsAsync(Guid serverId, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await ctx.JellyfinDestinationRoots
			.AsNoTracking()
			.Where(r => r.ServerId == serverId)
			.OrderBy(r => r.Name)
			.ToListAsync(ct)
			.ConfigureAwait(false);
	}

	public async Task<JellyfinDestinationRoot> CreateDestinationRootAsync(JellyfinDestinationRootDto dto, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var root = new JellyfinDestinationRoot
		{
			Name = dto.Name,
			ServerId = dto.ServerId,
			RootPath = dto.RootPath,
		};
		ctx.JellyfinDestinationRoots.Add(root);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		return root;
	}

	public async Task<JellyfinDestinationRoot?> UpdateDestinationRootAsync(Guid id, JellyfinDestinationRootDto dto, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var root = await ctx.JellyfinDestinationRoots.FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
		if (root is null)
		{
			return null;
		}

		root.Name = dto.Name;
		root.ServerId = dto.ServerId;
		root.RootPath = dto.RootPath;

		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		return root;
	}

	public async Task<bool> DeleteDestinationRootAsync(Guid id, CancellationToken ct = default)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var root = await ctx.JellyfinDestinationRoots.FirstOrDefaultAsync(r => r.Id == id, ct).ConfigureAwait(false);
		if (root is null)
		{
			return false;
		}

		ctx.JellyfinDestinationRoots.Remove(root);
		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
		return true;
	}

	public string DecryptApiKey(string encryptedApiKey) =>
		this._protector.Unprotect(encryptedApiKey);

	public string? DecryptSftpPassword(string? encryptedPassword) =>
		!string.IsNullOrEmpty(encryptedPassword) ? this._protector.Unprotect(encryptedPassword) : null;
}
