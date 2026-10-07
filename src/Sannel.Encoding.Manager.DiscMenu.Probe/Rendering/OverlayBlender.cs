using System.Runtime.InteropServices;
using SkiaSharp;

namespace Sannel.Encoding.Manager.DiscMenu.Probe.Rendering;

/// <summary>Draws a saved BD-J graphics plane (transparent PNG) over a video frame.</summary>
internal static class OverlayBlender
{
	/// <summary>Returns a copy of the BGRA <paramref name="frame"/> with the overlay PNG drawn over it, scaled to fit.</summary>
	public static byte[] Blend(byte[] frame, int width, int height, string overlayPath)
	{
		var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
		var result = (byte[])frame.Clone();
		using var overlay = SKBitmap.Decode(overlayPath);
		if (overlay is null)
		{
			return result;
		}

		var pinned = GCHandle.Alloc(result, GCHandleType.Pinned);
		try
		{
			using var target = new SKBitmap();
			target.InstallPixels(info, pinned.AddrOfPinnedObject(), info.RowBytes);
			using var canvas = new SKCanvas(target);
			using var image = SKImage.FromBitmap(overlay);
			canvas.DrawImage(image, new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
			canvas.Flush();
		}
		finally
		{
			pinned.Free();
		}

		// The video frame is opaque; keep it that way after blending.
		for (var i = 3; i < result.Length; i += 4)
		{
			result[i] = 255;
		}

		return result;
	}
}
