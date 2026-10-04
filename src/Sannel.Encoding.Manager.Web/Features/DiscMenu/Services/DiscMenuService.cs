using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.DiscMenu.Model;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.DiscMenu.Dto;
using Sannel.Encoding.Manager.Web.Features.DiscMenu.Entities;
using Sannel.Encoding.Manager.Web.Features.DiscMenu.Options;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Services;
using Sannel.Encoding.Manager.Web.Features.Scan.Dto;
using Sannel.Encoding.Manager.Web.Features.Scan.Services;

namespace Sannel.Encoding.Manager.Web.Features.DiscMenu.Services;

/// <inheritdoc />
public sealed class DiscMenuService : IDiscMenuService, IDisposable
{
	private static readonly TimeSpan _scanWait = TimeSpan.FromMinutes(10);

	private readonly IDbContextFactory<AppDbContext> _dbFactory;
	private readonly IFilesystemService _filesystemService;
	private readonly IBackgroundScanCoordinator _scanCoordinator;
	private readonly IDiscMenuProbeRunner _probeRunner;
	private readonly IHostApplicationLifetime _lifetime;
	private readonly DiscMenuOptions _options;
	private readonly ILogger<DiscMenuService> _logger;
	private readonly ConcurrentDictionary<string, Job> _jobs = new(StringComparer.Ordinal);
	private readonly SemaphoreSlim _concurrency;
	private readonly object _startLock = new();

	public DiscMenuService(
		IDbContextFactory<AppDbContext> dbFactory,
		IFilesystemService filesystemService,
		IBackgroundScanCoordinator scanCoordinator,
		IDiscMenuProbeRunner probeRunner,
		IHostApplicationLifetime lifetime,
		IOptions<DiscMenuOptions> options,
		ILogger<DiscMenuService> logger)
	{
		this._dbFactory = dbFactory;
		this._filesystemService = filesystemService;
		this._scanCoordinator = scanCoordinator;
		this._probeRunner = probeRunner;
		this._lifetime = lifetime;
		this._options = options.Value;
		this._logger = logger;
		this._concurrency = new SemaphoreSlim(Math.Max(1, this._options.MaxConcurrentProbes));
	}

	/// <inheritdoc />
	public async Task<DiscMenuJobStatus> StartOrGetAsync(string rootLabel, string relativePath, bool forceRefresh, TimeSpan maxWait, CancellationToken ct = default)
	{
		var physical = this._filesystemService.ResolvePhysicalPath(rootLabel, relativePath);
		var discType = DetectDiscType(physical)
			?? throw new ArgumentException("The path is not a DVD (VIDEO_TS) or Blu-ray (BDMV) folder.", nameof(relativePath));

		if (!this._options.Enabled)
		{
			return new DiscMenuJobStatus { State = DiscMenuJobState.Unavailable, Error = "Disc menu inspection is disabled (DiscMenu:Enabled)." };
		}

		if (!forceRefresh && await this.LoadCachedAsync(physical, ct) is { } cached)
		{
			return cached;
		}

		Job job;
		lock (this._startLock)
		{
			if (!this._jobs.TryGetValue(physical, out job!) || (job.Task.IsCompleted && (forceRefresh || job.Task.Result.State != DiscMenuJobState.Completed)))
			{
				job = new Job(DateTimeOffset.UtcNow);
				job.Task = Task.Run(() => this.InspectAsync(rootLabel, relativePath, physical, discType, job.StartedAt), CancellationToken.None);
				this._jobs[physical] = job;
			}
		}

		if (!job.Task.IsCompleted && maxWait > TimeSpan.Zero)
		{
			try
			{
				await job.Task.WaitAsync(maxWait, ct);
			}
			catch (TimeoutException)
			{
				// Still running — the caller polls.
			}
		}

		return job.Task.IsCompleted
			? job.Task.Result
			: new DiscMenuJobStatus { State = DiscMenuJobState.Inspecting, StartedAt = job.StartedAt };
	}

	/// <inheritdoc />
	public async Task<DiscMenuJobStatus?> GetStatusAsync(string rootLabel, string relativePath, CancellationToken ct = default)
	{
		var physical = this._filesystemService.ResolvePhysicalPath(rootLabel, relativePath);
		if (this._jobs.TryGetValue(physical, out var job))
		{
			return job.Task.IsCompleted
				? job.Task.Result
				: new DiscMenuJobStatus { State = DiscMenuJobState.Inspecting, StartedAt = job.StartedAt };
		}

		return await this.LoadCachedAsync(physical, ct);
	}

	/// <inheritdoc />
	public async Task<(byte[] Png, MenuNode Menu)?> GetScreenshotAsync(string rootLabel, string relativePath, string menuId, bool annotated, CancellationToken ct = default)
	{
		var status = await this.GetStatusAsync(rootLabel, relativePath, ct);
		if (status is not { State: DiscMenuJobState.Completed, Map: { } map, ScreenshotFolder: { } folder })
		{
			return null;
		}

		var menu = map.Menus.FirstOrDefault(m => string.Equals(m.Id, menuId, StringComparison.OrdinalIgnoreCase));
		var file = menu is null ? null : (annotated ? menu.Screenshot.AnnotatedFile : menu.Screenshot.File);
		if (menu is null || file is null)
		{
			return null;
		}

		var path = Path.Combine(folder, Path.GetFileName(file));
		return File.Exists(path) ? (await File.ReadAllBytesAsync(path, ct), menu) : null;
	}

	private async Task<DiscMenuJobStatus> InspectAsync(string rootLabel, string relativePath, string physical, string discType, DateTimeOffset startedAt)
	{
		var token = this._lifetime.ApplicationStopping;
		await this._concurrency.WaitAsync(token);
		try
		{
			// Blu-ray playlists are mapped to HandBrake titles through the scan; for DVDs it only validates titles.
			var scan = await this._scanCoordinator.StartOrGetAsync(rootLabel, relativePath, false, _scanWait, token);
			var folder = this.ScreenshotFolderFor(physical);
			if (Directory.Exists(folder))
			{
				Directory.Delete(folder, recursive: true);
			}

			Directory.CreateDirectory(folder);
			var result = await this._probeRunner.RunAsync(physical, discType, folder, token);
			if (result.Map is not { } map)
			{
				return new DiscMenuJobStatus
				{
					State = result.Unavailable ? DiscMenuJobState.Unavailable : DiscMenuJobState.Failed,
					StartedAt = startedAt,
					Error = result.Error,
				};
			}

			HandBrakeTitleMapper.Fill(map, scan.State == BackgroundJobState.Completed ? scan.Result?.Titles : null);
			await this.SaveCacheAsync(physical, map, folder, token);
			return new DiscMenuJobStatus { State = DiscMenuJobState.Completed, StartedAt = startedAt, Map = map, ScreenshotFolder = folder };
		}
		catch (Exception ex)
		{
			this._logger.LogError(ex, "Disc menu inspection failed for {Path}", physical);
			return new DiscMenuJobStatus { State = DiscMenuJobState.Failed, StartedAt = startedAt, Error = ex.Message };
		}
		finally
		{
			this._concurrency.Release();
		}
	}

	private async Task<DiscMenuJobStatus?> LoadCachedAsync(string physical, CancellationToken ct)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct);
		var row = await ctx.DiscMenuCache.AsNoTracking().FirstOrDefaultAsync(c => c.InputPath == physical, ct);
		if (row is null || row.ProbeVersion != DiscMenuMap.CurrentProbeVersion)
		{
			return null;
		}

		var map = DiscMenuJson.Deserialize(row.MenuJson);
		return map is null
			? null
			: new DiscMenuJobStatus { State = DiscMenuJobState.Completed, StartedAt = row.CachedAt, Map = map, ScreenshotFolder = row.ScreenshotFolder };
	}

	private async Task SaveCacheAsync(string physical, DiscMenuMap map, string folder, CancellationToken ct)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct);
		var row = await ctx.DiscMenuCache.FirstOrDefaultAsync(c => c.InputPath == physical, ct);
		if (row is null)
		{
			row = new DiscMenuCache { InputPath = physical };
			ctx.DiscMenuCache.Add(row);
		}

		row.DiscType = map.DiscType;
		row.MenuJson = DiscMenuJson.Serialize(map);
		row.ScreenshotFolder = folder;
		row.ProbeVersion = map.ProbeVersion;
		row.CachedAt = DateTimeOffset.UtcNow;
		await ctx.SaveChangesAsync(ct);
	}

	private string ScreenshotFolderFor(string physical)
	{
		var root = Path.IsPathRooted(this._options.OutputPath)
			? this._options.OutputPath
			: Path.Combine(this._options.ContentRootPath, this._options.OutputPath);
		var name = Convert.ToHexStringLower(SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(physical)));
		return Path.Combine(root, name);
	}

	/// <summary>"dvd" for a VIDEO_TS rip, "bluray" for a BDMV rip, else null.</summary>
	internal static string? DetectDiscType(string physical)
	{
		if (!Directory.Exists(physical))
		{
			return null;
		}

		var name = Path.GetFileName(physical.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
		if (name.Equals("VIDEO_TS", StringComparison.OrdinalIgnoreCase) || Directory.Exists(Path.Combine(physical, "VIDEO_TS")))
		{
			return "dvd";
		}

		return name.Equals("BDMV", StringComparison.OrdinalIgnoreCase) || Directory.Exists(Path.Combine(physical, "BDMV"))
			? "bluray"
			: null;
	}

	public void Dispose() => this._concurrency.Dispose();

	private sealed class Job(DateTimeOffset startedAt)
	{
		public DateTimeOffset StartedAt { get; } = startedAt;

		public Task<DiscMenuJobStatus> Task { get; set; } = default!;
	}
}
