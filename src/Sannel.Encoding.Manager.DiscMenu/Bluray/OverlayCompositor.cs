using Sannel.Encoding.Manager.DiscMenu.Native;

namespace Sannel.Encoding.Manager.DiscMenu.Bluray;

/// <summary>
/// Keeps a down-sampled copy of the Blu-ray interactive-graphics plane (HDMV IG or BD-J ARGB) so menu states can
/// be fingerprinted and compared. Only every <see cref="Step"/>-th pixel is stored; that is plenty to tell which
/// button is highlighted and where it is.
/// </summary>
internal sealed unsafe class OverlayCompositor
{
	/// <summary>Sampling step in pixels.</summary>
	public const int Step = 4;

	private readonly object _gate = new();
	private uint[] _plane = [];
	private int _width;
	private int _height;
	private long _flushCount;
	private bool _hidden = true;

	public int Width => this._width * Step;

	public int Height => this._height * Step;

	public long FlushCount => Interlocked.Read(ref this._flushCount);

	public bool IsVisible
	{
		get
		{
			lock (this._gate)
			{
				return !this._hidden && this._plane.Any(p => p != 0);
			}
		}
	}

	/// <summary>Hash of the current plane contents (0 when hidden or empty).</summary>
	public ulong Fingerprint()
	{
		lock (this._gate)
		{
			if (this._hidden || this._plane.Length == 0)
			{
				return 0;
			}

			// FNV-1a over the sampled pixels.
			var hash = 14695981039346656037UL;
			foreach (var pixel in this._plane)
			{
				hash = (hash ^ pixel) * 1099511628211UL;
			}

			return hash;
		}
	}

	/// <summary>A copy of the sampled plane for diffing.</summary>
	public uint[] Snapshot()
	{
		lock (this._gate)
		{
			return this._hidden ? new uint[this._plane.Length] : (uint[])this._plane.Clone();
		}
	}

	public (int Width, int Height) SampledSize
	{
		get
		{
			lock (this._gate)
			{
				return (this._width, this._height);
			}
		}
	}

	public void OnOverlay(BlurayNative.BdOverlay* ov)
	{
		// Only the interactive-graphics plane carries menus.
		if (ov->Plane != 1)
		{
			return;
		}

		lock (this._gate)
		{
			switch (ov->Cmd)
			{
				case BlurayNative.OverlayInit:
					this.Init(ov->W, ov->H);
					break;
				case BlurayNative.OverlayClose:
					this._hidden = true;
					Array.Clear(this._plane);
					break;
				case BlurayNative.OverlayClear:
					Array.Clear(this._plane);
					break;
				case BlurayNative.OverlayWipe:
					this.Fill(ov->X, ov->Y, ov->W, ov->H, 0);
					break;
				case BlurayNative.OverlayHide:
					this._hidden = true;
					break;
				case BlurayNative.OverlayDraw:
					this._hidden = false;
					this.DrawRle(ov);
					break;
				case BlurayNative.OverlayFlush:
					Interlocked.Increment(ref this._flushCount);
					break;
			}
		}
	}

	public void OnArgbOverlay(BlurayNative.BdArgbOverlay* ov)
	{
		if (ov->Plane != 1)
		{
			return;
		}

		lock (this._gate)
		{
			switch (ov->Cmd)
			{
				case BlurayNative.OverlayInit:
					this.Init(ov->W, ov->H);
					break;
				case BlurayNative.OverlayClose:
					this._hidden = true;
					Array.Clear(this._plane);
					break;
				case BlurayNative.OverlayDraw:
					this._hidden = false;
					this.DrawArgb(ov);
					break;
				case BlurayNative.OverlayFlush:
					Interlocked.Increment(ref this._flushCount);
					break;
			}
		}
	}

	private void Init(int width, int height)
	{
		this._width = Math.Max(1, width / Step);
		this._height = Math.Max(1, height / Step);
		this._plane = new uint[this._width * this._height];
		this._hidden = true;
	}

	private void Fill(int x, int y, int w, int h, uint value)
	{
		for (var py = y; py < y + h; py++)
		{
			if (py % Step != 0)
			{
				continue;
			}

			for (var px = x; px < x + w; px++)
			{
				if (px % Step == 0)
				{
					this.Set(px, py, value);
				}
			}
		}
	}

	private void Set(int px, int py, uint value)
	{
		var sx = px / Step;
		var sy = py / Step;
		if (sx < this._width && sy < this._height)
		{
			this._plane[(sy * this._width) + sx] = value;
		}
	}

	private void DrawRle(BlurayNative.BdOverlay* ov)
	{
		if (ov->Img == IntPtr.Zero || ov->Palette == IntPtr.Zero || this._plane.Length == 0)
		{
			return;
		}

		// RLE elements: { uint16 len; uint16 color } until each row's lengths add up to w.
		var rle = (ushort*)ov->Img;
		var palette = (uint*)ov->Palette; // { Y, Cr, Cb, T } per entry
		for (var row = 0; row < ov->H; row++)
		{
			var py = ov->Y + row;
			for (var col = 0; col < ov->W;)
			{
				int length = rle[0];
				var color = rle[1];
				rle += 2;
				if (length == 0)
				{
					break;
				}

				if (py % Step == 0)
				{
					var value = palette[color & 0xff];
					for (var i = 0; i < length; i++)
					{
						var px = ov->X + col + i;
						if (px % Step == 0)
						{
							this.Set(px, py, value);
						}
					}
				}

				col += length;
			}
		}
	}

	private void DrawArgb(BlurayNative.BdArgbOverlay* ov)
	{
		if (ov->Argb == IntPtr.Zero || this._plane.Length == 0)
		{
			return;
		}

		var argb = (uint*)ov->Argb;
		for (var row = 0; row < ov->H; row++)
		{
			var py = ov->Y + row;
			if (py % Step != 0)
			{
				continue;
			}

			for (var col = 0; col < ov->W; col++)
			{
				var px = ov->X + col;
				if (px % Step == 0)
				{
					this.Set(px, py, argb[(row * ov->Stride) + col]);
				}
			}
		}
	}
}
