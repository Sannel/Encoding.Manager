using System.Collections.Concurrent;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Manager.Web.Features.Filesystem.Services;
using Sannel.Encoding.Manager.Web.Features.Scan.Dto;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Services;

/// <inheritdoc />
public class BackgroundScanCoordinator : IBackgroundScanCoordinator
{
	private static readonly TimeSpan _retention = TimeSpan.FromMinutes(30);

	private readonly IFilesystemService _filesystemService;
	// Resolved lazily: HandBrakeService throws from its constructor when HandBrakeCLI is missing, and that
	// must only fail scans — not every tool that happens to depend on this coordinator.
	private readonly IServiceProvider _services;
	private readonly IHostApplicationLifetime _lifetime;
	private readonly ILogger<BackgroundScanCoordinator> _logger;
	private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
	private readonly object _startLock = new();

	public BackgroundScanCoordinator(
		IFilesystemService filesystemService,
		IServiceProvider services,
		IHostApplicationLifetime lifetime,
		ILogger<BackgroundScanCoordinator> logger)
	{
		this._filesystemService = filesystemService;
		this._services = services;
		this._lifetime = lifetime;
		this._logger = logger;
	}

	/// <inheritdoc />
	public async Task<ScanJobStatus> StartOrGetAsync(string rootLabel, string relativePath, bool forceRescan, TimeSpan maxWait, CancellationToken ct = default)
	{
		var physicalPath = this._filesystemService.ResolvePhysicalPath(rootLabel, relativePath);
		this.EvictExpired();

		Entry entry;
		lock (this._startLock)
		{
			// Re-run when forced, or when the last attempt failed (e.g. HandBrake was installed since).
			if (!this._entries.TryGetValue(physicalPath, out entry!)
				|| (entry.Task.IsCompleted && (forceRescan || !entry.Task.IsCompletedSuccessfully || !entry.Task.Result.IsSuccess)))
			{
				entry = this.Start(physicalPath, forceRescan);
				this._entries[physicalPath] = entry;
			}
		}

		if (!entry.Task.IsCompleted && maxWait > TimeSpan.Zero)
		{
			try
			{
				await entry.Task.WaitAsync(maxWait, ct);
			}
			catch (TimeoutException)
			{
				// Still running — the caller polls.
			}
			catch (Exception) when (entry.Task.IsCompleted)
			{
				// The scan itself failed; ToStatus reports it as Failed.
			}
		}

		return entry.ToStatus(physicalPath);
	}

	/// <inheritdoc />
	public ScanJobStatus? GetStatus(string rootLabel, string relativePath)
	{
		var physicalPath = this._filesystemService.ResolvePhysicalPath(rootLabel, relativePath);
		this.EvictExpired();
		return this._entries.TryGetValue(physicalPath, out var entry) ? entry.ToStatus(physicalPath) : null;
	}

	private Entry Start(string physicalPath, bool forceRescan)
	{
		var entry = new Entry { StartedAt = DateTimeOffset.UtcNow };
		var token = this._lifetime.ApplicationStopping;
		entry.Task = Task.Run(async () =>
		{
			try
			{
				var handBrake = this._services.GetRequiredService<IHandBrakeService>();
				var result = await handBrake.ScanAsync(physicalPath, forceRescan, token);
				entry.CompletedAt = DateTimeOffset.UtcNow;
				return result;
			}
			catch (Exception ex)
			{
				this._logger.LogError(ex, "Background scan failed for {Path}", physicalPath);
				entry.CompletedAt = DateTimeOffset.UtcNow;
				throw;
			}
		}, CancellationToken.None);
		return entry;
	}

	private void EvictExpired()
	{
		var cutoff = DateTimeOffset.UtcNow - _retention;
		foreach (var (key, entry) in this._entries)
		{
			if (entry.CompletedAt is { } completed && completed < cutoff)
			{
				this._entries.TryRemove(key, out _);
			}
		}
	}

	private sealed class Entry
	{
		public Task<HandBrakeScanResult> Task { get; set; } = default!;

		public DateTimeOffset StartedAt { get; init; }

		public DateTimeOffset? CompletedAt { get; set; }

		public ScanJobStatus ToStatus(string physicalPath)
		{
			if (!this.Task.IsCompleted)
			{
				return new ScanJobStatus { State = BackgroundJobState.Running, PhysicalPath = physicalPath, StartedAt = this.StartedAt };
			}

			if (this.Task.IsCompletedSuccessfully)
			{
				var result = this.Task.Result;
				return result.IsSuccess
					? new ScanJobStatus { State = BackgroundJobState.Completed, PhysicalPath = physicalPath, StartedAt = this.StartedAt, CompletedAt = this.CompletedAt, Result = result }
					: new ScanJobStatus { State = BackgroundJobState.Failed, PhysicalPath = physicalPath, StartedAt = this.StartedAt, CompletedAt = this.CompletedAt, Error = result.Error?.Message ?? "HandBrake scan failed." };
			}

			return new ScanJobStatus
			{
				State = BackgroundJobState.Failed,
				PhysicalPath = physicalPath,
				StartedAt = this.StartedAt,
				CompletedAt = this.CompletedAt,
				Error = this.Task.Exception?.GetBaseException().Message ?? "Scan was cancelled.",
			};
		}
	}
}
