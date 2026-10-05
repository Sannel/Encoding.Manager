using Sannel.Encoding.Manager.DiscMenu.Model;
using SkiaSharp;

namespace Sannel.Encoding.Manager.DiscMenu.Probe.Rendering;

/// <summary>Saves menu frames as PNGs, plus a copy with each button outlined and numbered.</summary>
internal static class ButtonAnnotator
{
	private static readonly SKColor _outline = new(255, 214, 0);
	private static readonly SKColor _focus = new(0, 229, 255);
	private static readonly SKColor _badge = new(20, 20, 20, 230);

	/// <summary>
	/// Scales a BGRA frame to <paramref name="width"/> (display aspect), writes the raw and annotated PNGs,
	/// and returns the saved size.
	/// </summary>
	public static (int Width, int Height) Save(
		byte[] bgra, int frameWidth, int frameHeight, MenuNode menu, int width, string rawPath, string annotatedPath, int? highlightedButton = null)
	{
		var aspect = menu.DisplayAspectRatio > 0 ? menu.DisplayAspectRatio : (double)frameWidth / frameHeight;
		var height = (int)Math.Round(width / aspect);

		var info = new SKImageInfo(frameWidth, frameHeight, SKColorType.Bgra8888, SKAlphaType.Opaque);
		using var source = new SKBitmap();
		var pinned = System.Runtime.InteropServices.GCHandle.Alloc(bgra, System.Runtime.InteropServices.GCHandleType.Pinned);
		try
		{
			source.InstallPixels(info, pinned.AddrOfPinnedObject(), info.RowBytes);
			using var scaled = source.Resize(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
			WritePng(scaled, rawPath);

			using var canvas = new SKCanvas(scaled);
			var sx = (float)width / frameWidth;
			var sy = (float)height / frameHeight;
			using var stroke = new SKPaint { Color = _outline, Style = SKPaintStyle.Stroke, StrokeWidth = 3, IsAntialias = true };
			using var fill = new SKPaint { Color = _badge, Style = SKPaintStyle.Fill, IsAntialias = true };
			using var digitPaint = new SKPaint { Color = _outline, Style = SKPaintStyle.Stroke, StrokeWidth = 3, StrokeCap = SKStrokeCap.Round, IsAntialias = true };

			foreach (var button in menu.Buttons)
			{
				var rect = new SKRect(button.Rect.X * sx, button.Rect.Y * sy, (button.Rect.X + button.Rect.W) * sx, (button.Rect.Y + button.Rect.H) * sy);
				if (button.Number == highlightedButton)
				{
					// The button this screenshot is about: a thick cyan outline so it stands out from the others.
					using var focus = new SKPaint { Color = _focus, Style = SKPaintStyle.Stroke, StrokeWidth = 6, IsAntialias = true };
					canvas.DrawRect(rect, focus);
				}
				else
				{
					canvas.DrawRect(rect, stroke);
				}

				var label = button.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
				var badge = new SKRect(rect.Left, rect.Top - 30, rect.Left + 10 + (label.Length * DigitAdvance), rect.Top);
				if (badge.Top < 0)
				{
					badge.Offset(0, rect.Height + 30);
				}

				canvas.DrawRoundRect(badge, 4, 4, fill);
				var x = badge.Left + 7;
				foreach (var digit in label)
				{
					DrawDigit(canvas, digit - '0', x, badge.Top + 5, digitPaint);
					x += DigitAdvance;
				}
			}

			canvas.Flush();
			WritePng(scaled, annotatedPath);
		}
		finally
		{
			pinned.Free();
		}

		return (width, height);
	}

	private const float DigitWidth = 10;
	private const float DigitHeight = 20;
	private const float DigitAdvance = 16;

	// Seven-segment masks (a b c d e f g) for 0-9: drawn as lines so no font is needed (headless Skia has none).
	private static readonly int[] _segments = [0b1111110, 0b0110000, 0b1101101, 0b1111001, 0b0110011, 0b1011011, 0b1011111, 0b1110000, 0b1111111, 0b1111011];

	private static void DrawDigit(SKCanvas canvas, int digit, float left, float top, SKPaint paint)
	{
		var mask = _segments[Math.Clamp(digit, 0, 9)];
		float l = left, r = left + DigitWidth, t = top, m = top + (DigitHeight / 2), b = top + DigitHeight;
		(float X0, float Y0, float X1, float Y1)[] lines =
		[
			(l, t, r, t), // a
			(r, t, r, m), // b
			(r, m, r, b), // c
			(l, b, r, b), // d
			(l, m, l, b), // e
			(l, t, l, m), // f
			(l, m, r, m), // g
		];
		for (var i = 0; i < 7; i++)
		{
			if ((mask & (1 << (6 - i))) != 0)
			{
				canvas.DrawLine(lines[i].X0, lines[i].Y0, lines[i].X1, lines[i].Y1, paint);
			}
		}
	}

	private static void WritePng(SKBitmap bitmap, string path)
	{
		using var image = SKImage.FromBitmap(bitmap);
		using var data = image.Encode(SKEncodedImageFormat.Png, 90);
		using var stream = File.Create(path);
		data.SaveTo(stream);
	}
}
