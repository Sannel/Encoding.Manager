using Microsoft.Extensions.Options;
using Sannel.Encoding.Manager.HandBrake;
using Sannel.Encoding.Manager.Web.Features.Interlace.Dto;
using Sannel.Encoding.Manager.Web.Features.Interlace.Options;

namespace Sannel.Encoding.Manager.Web.Features.Interlace.Services;

/// <inheritdoc />
public sealed class FfmpegLocator : IFfmpegLocator
{
	private readonly IProcessRunner _runner;
	private readonly InterlaceOptions _options;
	private readonly ILogger<FfmpegLocator> _logger;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private FfmpegInfo? _info;

	public FfmpegLocator(IProcessRunner runner, IOptions<InterlaceOptions> options, ILogger<FfmpegLocator> logger)
	{
		this._runner = runner;
		this._options = options.Value;
		this._logger = logger;
	}

	/// <inheritdoc />
	public async Task<FfmpegInfo> GetAsync(CancellationToken ct = default)
	{
		if (this._info is { } cached)
		{
			return cached;
		}

		await this._gate.WaitAsync(ct);
		try
		{
			this._info ??= await this.DetectAsync(ct);
			return this._info;
		}
		finally
		{
			this._gate.Release();
		}
	}

	private async Task<FfmpegInfo> DetectAsync(CancellationToken ct)
	{
		var fileName = string.IsNullOrWhiteSpace(this._options.FfmpegPath) ? "ffmpeg" : this._options.FfmpegPath;
		try
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
			timeout.CancelAfter(TimeSpan.FromSeconds(30));
			var version = await this._runner.RunAsync(fileName, ["-hide_banner", "-version"], timeout.Token);
			var protocols = await this._runner.RunAsync(fileName, ["-hide_banner", "-protocols"], timeout.Token);
			var versionLine = version.StandardOutput.Split('\n', 2)[0].Trim();
			var bluray = protocols.StandardOutput.Split('\n').Any(l => l.Trim() == "bluray");
			var info = new FfmpegInfo(true, bluray, fileName, versionLine, bluray ? null : "ffmpeg has no 'bluray' protocol (built without libbluray); Blu-ray titles use HandBrake's flag only.");
			if (bluray)
			{
				this._logger.LogInformation("Interlace detection uses {Ffmpeg} ({Version})", fileName, versionLine);
			}
			else
			{
				this._logger.LogWarning("Interlace detection: {Ffmpeg} ({Version}) has no 'bluray' protocol; Blu-ray titles fall back to HandBrake's interlace flag", fileName, versionLine);
			}

			return info;
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
		{
			this._logger.LogWarning("Interlace detection: ffmpeg not found ({Ffmpeg}: {Message}). Set Interlace:FfmpegPath. Blu-ray titles fall back to HandBrake's flag; media files report unknown", fileName, ex.Message);
			return new FfmpegInfo(false, false, fileName, string.Empty, $"ffmpeg not found ({ex.Message}). Set Interlace:FfmpegPath.");
		}
	}
}
