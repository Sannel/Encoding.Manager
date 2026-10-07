namespace Sannel.Encoding.Manager.Web.Features.Interlace.Dto;

/// <summary>Frame counts from ffmpeg's <c>idet</c> filter ("Multi frame detection" and "Repeated Fields" lines).</summary>
public sealed record IdetCounts(int Tff, int Bff, int Progressive, int Undetermined, int RepeatedNeither, int RepeatedTop, int RepeatedBottom)
{
	public static IdetCounts Empty { get; } = new(0, 0, 0, 0, 0, 0, 0);

	/// <summary>Frames idet decided on (TFF + BFF + Progressive).</summary>
	public int Decided => this.Tff + this.Bff + this.Progressive;

	/// <summary>Frames counted by the repeated-field detector.</summary>
	public int RepeatedTotal => this.RepeatedNeither + this.RepeatedTop + this.RepeatedBottom;

	/// <summary>(TFF + BFF) / decided frames × 100, or 0 when nothing was decided.</summary>
	public double InterlacedPercent => this.Decided == 0 ? 0 : (this.Tff + this.Bff) * 100.0 / this.Decided;

	/// <summary>Frames with a repeated field / all frames × 100, or 0 when nothing was counted.</summary>
	public double TelecinePercent => this.RepeatedTotal == 0 ? 0 : (this.RepeatedTop + this.RepeatedBottom) * 100.0 / this.RepeatedTotal;

	public IdetCounts Add(IdetCounts other) => new(
		this.Tff + other.Tff,
		this.Bff + other.Bff,
		this.Progressive + other.Progressive,
		this.Undetermined + other.Undetermined,
		this.RepeatedNeither + other.RepeatedNeither,
		this.RepeatedTop + other.RepeatedTop,
		this.RepeatedBottom + other.RepeatedBottom);
}
