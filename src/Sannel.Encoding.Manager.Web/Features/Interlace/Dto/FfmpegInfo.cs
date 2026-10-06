namespace Sannel.Encoding.Manager.Web.Features.Interlace.Dto;

/// <summary>What was found about the configured ffmpeg binary.</summary>
public sealed record FfmpegInfo(bool IsAvailable, bool SupportsBluray, string FileName, string Version, string? Problem);
