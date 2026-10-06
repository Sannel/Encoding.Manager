using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.DiscMenu.Services;
using Sannel.Encoding.Manager.Web.Features.Interlace.Dto;
using Sannel.Encoding.Manager.Web.Features.Interlace.Entities;
using Sannel.Encoding.Manager.Web.Features.Interlace.Options;

namespace Sannel.Encoding.Manager.Web.Features.Interlace.Services;

/// <inheritdoc />
public sealed class InterlaceService : IInterlaceService, IDisposable
{
	/// <summary>Bump when the probe or thresholds change so cached rows are re-probed.</summary>
	public const int CurrentProbeVersion = 1;

	/// <summary>A failed probe is not retried for this long (the fallback verdict is reported meanwhile).</summary>
	private static readonly TimeSpan _failureRetention = TimeSpan.FromMinutes(30);

	private readonly IDbContextFactory<AppDbContext> _dbFactory;
	private readonly IInterlaceProbeService _probe;
	private readonly IFfmpegLocator _locator;
	private readonly InterlaceClassifier _classifier;
	private readonly InterlaceOptions _options;
	private readonly IHostApplicationLifetime _lifetime;
	private readonly ILogger<InterlaceService> _logger;
	private readonly SemaphoreSlim _concurrency;
	private readonly ConcurrentDictionary<(string Path, int Playlist), Task> _inFlight = new();
	private readonly ConcurrentDictionary<(string Path, int Playlist), DateTimeOffset> _failed = new();

	public InterlaceService(
		IDbContextFactory<AppDbContext> dbFactory,
		IInterlaceProbeService probe,
		IFfmpegLocator locator,
		InterlaceClassifier classifier,
		IOptions<InterlaceOptions> options,
		IHostApplicationLifetime lifetime,
		ILogger<InterlaceService> logger)
	{
		this._dbFactory = dbFactory;
		this._probe = probe;
		this._locator = locator;
		this._classifier = classifier;
		this._options = options.Value;
		this._lifetime = lifetime;
		this._logger = logger;
		this._concurrency = new SemaphoreSlim(Math.Max(1, this._options.MaxConcurrentProbes));
	}

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<int, InterlaceResult>> GetDiscTitlesAsync(string discPhysicalPath, IReadOnlyList<TitleInfo> titles, CancellationToken ct = default)
	{
		var results = new Dictionary<int, InterlaceResult>();
		if (DiscMenuService.DetectDiscType(discPhysicalPath) == "dvd")
		{
			foreach (var title in titles)
			{
				results[title.TitleNumber] = this._classifier.ForDvd();
			}

			return results;
		}

		var ffmpeg = await this._locator.GetAsync(ct);
		var probe = this._options.Enabled && ffmpeg.SupportsBluray;
		var cached = probe ? await this.LoadAsync(discPhysicalPath, null, ct) : [];
		foreach (var title in titles)
		{
			if (!probe || title.Playlist is not { } playlist)
			{
				results[title.TitleNumber] = this._classifier.ForBlurayTitle(null, title.InterlaceDetected);
				continue;
			}

			var key = (discPhysicalPath, playlist);
			var stamp = BlurayStamp(discPhysicalPath, playlist);
			if (cached.TryGetValue(key, out var row) && IsValid(row, stamp))
			{
				results[title.TitleNumber] = this._classifier.ForBlurayTitle(ToMeasurement(row), title.InterlaceDetected);
			}
			else if (this.RecentlyFailed(key))
			{
				results[title.TitleNumber] = this._classifier.ForBlurayTitle(null, title.InterlaceDetected);
			}
			else
			{
				var duration = title.Duration;
				var handBrakeDetected = title.InterlaceDetected;
				this.Enqueue(key, stamp, handBrakeDetected, token => this._probe.ProbeBlurayTitleAsync(discPhysicalPath, playlist, duration, token));
				results[title.TitleNumber] = this._classifier.Pending();
			}
		}

		return results;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyDictionary<string, InterlaceResult>> GetFilesAsync(IReadOnlyList<string> filePhysicalPaths, CancellationToken ct = default)
	{
		var results = new Dictionary<string, InterlaceResult>(StringComparer.Ordinal);
		var ffmpeg = await this._locator.GetAsync(ct);
		if (!this._options.Enabled || !ffmpeg.IsAvailable)
		{
			foreach (var path in filePhysicalPaths)
			{
				results[path] = this._classifier.ForFile(null);
			}

			return results;
		}

		var cached = await this.LoadAsync(null, filePhysicalPaths, ct);
		foreach (var path in filePhysicalPaths)
		{
			var key = (path, InterlaceProbeCache.FilePlaylist);
			var stamp = FileStamp(path);
			if (cached.TryGetValue(key, out var row) && IsValid(row, stamp))
			{
				results[path] = this._classifier.ForFile(ToMeasurement(row));
			}
			else if (this.RecentlyFailed(key) || stamp is null)
			{
				results[path] = this._classifier.ForFile(null);
			}
			else
			{
				this.Enqueue(key, stamp, null, token => this._probe.ProbeFileAsync(path, token));
				results[path] = this._classifier.Pending();
			}
		}

		return results;
	}

	/// <summary>Starts a background probe for <paramref name="key"/> unless one is already running.</summary>
	private void Enqueue((string Path, int Playlist) key, (long Size, DateTimeOffset LastWrite)? stamp, bool? handBrakeDetected, Func<CancellationToken, Task<InterlaceMeasurement?>> probe)
	{
		if (this._inFlight.ContainsKey(key))
		{
			return;
		}

		var gate = new TaskCompletionSource();
		if (!this._inFlight.TryAdd(key, gate.Task))
		{
			return;
		}

		_ = Task.Run(async () =>
		{
			var token = this._lifetime.ApplicationStopping;
			try
			{
				await this._concurrency.WaitAsync(token);
				try
				{
					var measurement = await probe(token);
					if (measurement is null)
					{
						this._failed[key] = DateTimeOffset.UtcNow;
						return;
					}

					var ffmpeg = await this._locator.GetAsync(token);
					await this.SaveAsync(key, stamp, measurement, handBrakeDetected, ffmpeg.Version, token);
				}
				finally
				{
					this._concurrency.Release();
				}
			}
			catch (OperationCanceledException) when (token.IsCancellationRequested)
			{
				// Shutting down.
			}
			catch (Exception ex)
			{
				this._logger.LogError(ex, "Interlace probe failed for {Path} playlist {Playlist}", key.Path, key.Playlist);
				this._failed[key] = DateTimeOffset.UtcNow;
			}
			finally
			{
				this._inFlight.TryRemove(key, out _);
				gate.TrySetResult();
			}
		}, CancellationToken.None);
	}

	private bool RecentlyFailed((string Path, int Playlist) key)
	{
		if (!this._failed.TryGetValue(key, out var at))
		{
			return false;
		}

		if (DateTimeOffset.UtcNow - at < _failureRetention)
		{
			return true;
		}

		this._failed.TryRemove(key, out _);
		return false;
	}

	private async Task<Dictionary<(string Path, int Playlist), InterlaceProbeCache>> LoadAsync(string? discPath, IReadOnlyList<string>? filePaths, CancellationToken ct)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct);
		var query = ctx.InterlaceProbeCache.AsNoTracking();
		query = discPath is not null
			? query.Where(c => c.SourcePath == discPath && c.Playlist != InterlaceProbeCache.FilePlaylist)
			: query.Where(c => filePaths!.Contains(c.SourcePath) && c.Playlist == InterlaceProbeCache.FilePlaylist);
		var rows = await query.ToListAsync(ct);
		return rows.ToDictionary(r => (r.SourcePath, r.Playlist));
	}

	private async Task SaveAsync((string Path, int Playlist) key, (long Size, DateTimeOffset LastWrite)? stamp, InterlaceMeasurement measurement, bool? handBrakeDetected, string ffmpegVersion, CancellationToken ct)
	{
		await using var ctx = await this._dbFactory.CreateDbContextAsync(ct);
		var row = await ctx.InterlaceProbeCache.FirstOrDefaultAsync(c => c.SourcePath == key.Path && c.Playlist == key.Playlist, ct);
		if (row is null)
		{
			row = new InterlaceProbeCache { Id = Guid.NewGuid(), SourcePath = key.Path, Playlist = key.Playlist };
			ctx.InterlaceProbeCache.Add(row);
		}

		row.SourceSize = stamp?.Size ?? 0;
		row.SourceLastWriteUtc = stamp?.LastWrite ?? DateTimeOffset.MinValue;
		row.Verdict = measurement.Verdict.ToString();
		row.InterlacedPercent = measurement.InterlacedPercent;
		row.TelecinePercent = measurement.TelecinePercent;
		row.SampledFrames = measurement.SampledFrames;
		row.HandBrakeDetected = handBrakeDetected;
		row.FfmpegVersion = ffmpegVersion.Length > 128 ? ffmpegVersion[..128] : ffmpegVersion;
		row.ProbeVersion = CurrentProbeVersion;
		row.ProbedAt = DateTimeOffset.UtcNow;
		await ctx.SaveChangesAsync(ct);
	}

	private static bool IsValid(InterlaceProbeCache row, (long Size, DateTimeOffset LastWrite)? stamp) =>
		row.ProbeVersion == CurrentProbeVersion
		&& stamp is { } s
		&& row.SourceSize == s.Size
		&& row.SourceLastWriteUtc == s.LastWrite;

	private static InterlaceMeasurement ToMeasurement(InterlaceProbeCache row) =>
		new(Enum.TryParse<InterlaceVerdict>(row.Verdict, out var v) ? v : InterlaceVerdict.Unknown, row.InterlacedPercent, row.TelecinePercent, row.SampledFrames);

	/// <summary>Size and last-write time of a media file (the cache invalidation stamp), or null when it is missing.</summary>
	private static (long Size, DateTimeOffset LastWrite)? FileStamp(string path)
	{
		var info = new FileInfo(path);
		return info.Exists ? (info.Length, new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero)) : null;
	}

	/// <summary>
	/// The stamp of a Blu-ray playlist: its BDMV/PLAYLIST/NNNNN.mpls file (re-ripping the disc rewrites it). Falls back
	/// to the disc folder's time when the playlist file is not found.
	/// </summary>
	private static (long Size, DateTimeOffset LastWrite)? BlurayStamp(string discPath, int playlist)
	{
		var mpls = Path.Combine(discPath, "BDMV", "PLAYLIST", playlist.ToString("00000", System.Globalization.CultureInfo.InvariantCulture) + ".mpls");
		if (File.Exists(mpls))
		{
			return FileStamp(mpls);
		}

		var folder = new DirectoryInfo(discPath);
		return folder.Exists ? (0, new DateTimeOffset(folder.LastWriteTimeUtc, TimeSpan.Zero)) : null;
	}

	public void Dispose() => this._concurrency.Dispose();
}
