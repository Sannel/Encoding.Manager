namespace Sannel.Encoding.Manager.Web.Features.Interlace.Dto;

/// <summary>The outcome of an ffmpeg <c>idet</c> probe of one title or file (before combining with HandBrake's flag).</summary>
public sealed record InterlaceMeasurement(InterlaceVerdict Verdict, double InterlacedPercent, double TelecinePercent, int SampledFrames);
