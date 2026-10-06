using System.Globalization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sannel.Encoding.Manager.Web.Features.Interlace.Dto;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Scan.Components;

/// <summary>A row's interlace verdict chip and its per-track preset selector (defaulting from the verdict).</summary>
public partial class InterlacePresetCell : ComponentBase
{
	/// <summary>The row's verdict; null or pending shows "Checking…".</summary>
	[Parameter]
	public InterlaceResult? Result { get; set; }

	[Parameter]
	public IReadOnlyList<EncodingPreset> Presets { get; set; } = [];

	/// <summary>The row's preset; null means the job preset.</summary>
	[Parameter]
	public string? Value { get; set; }

	[Parameter]
	public EventCallback<string?> ValueChanged { get; set; }

	private string Label => this.Result?.Verdict switch
	{
		InterlaceVerdict.Interlaced => "Interlaced",
		InterlaceVerdict.Telecined => "Telecined",
		InterlaceVerdict.Mixed => "Mixed",
		InterlaceVerdict.Progressive => "Progressive",
		_ => "Unknown",
	};

	private Color ChipColor => this.Result?.NeedsDecomb == true ? Color.Warning : Color.Default;

	private Variant ChipVariant => this.Result?.NeedsDecomb == true ? Variant.Filled : Variant.Outlined;

	private string Tooltip
	{
		get
		{
			if (this.Result is null)
			{
				return string.Empty;
			}

			var source = this.Result.Source switch
			{
				"dvd" => "DVD — always treated as interlaced",
				"handbrake+ffmpeg" => "HandBrake comb check + ffmpeg idet",
				"handbrake" => "HandBrake comb check only (ffmpeg unavailable)",
				"ffmpeg" => "ffmpeg idet",
				_ => "not determined",
			};
			var numbers = this.Result.InterlacedPercent is { } interlaced
				? string.Format(CultureInfo.InvariantCulture, " · {0:0.#}% interlaced, {1:0.#}% telecine", interlaced, this.Result.TelecinePercent ?? 0)
				: string.Empty;
			var preset = this.Result.RecommendedPreset is { } recommended ? $" · suggests \"{recommended}\"" : string.Empty;
			return source + numbers + preset;
		}
	}
}
