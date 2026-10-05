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

	/// <summary>
	/// HDMV: the plane holds palette index + 1 (0 = transparent) and colours are resolved through the current
	/// palette, because HDMV menus often show a button's highlight by changing only the palette
	/// (palette_update_flag, no new pixels). BD-J: the plane holds ARGB values.
	/// </summary>
	private readonly uint[] _palette = new uint[256];
	private bool _indexed;
	private uint[] _plane = [];

	/// <summary>Full-resolution copy of the BD-J (ARGB) plane, kept so menu screenshots can show its graphics.</summary>
	private uint[]? _full;
	private int _fullWidth;
	private int _fullHeight;
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
				return !this._hidden && this._plane.Any(p => this.Resolve(p) != 0);
			}
		}
	}

	/// <summary>The colour of a stored plane value (0 = transparent).</summary>
	private uint Resolve(uint value)
	{
		if (!this._indexed)
		{
			return value;
		}

		if (value == 0)
		{
			return 0;
		}

		// Palette entries are { Y, Cr, Cb, T }; T (the top byte) = 0 is fully transparent.
		var entry = this._palette[(value - 1) & 0xff];
		return (entry >> 24) == 0 ? 0 : entry;
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

			// FNV-1a. BD-J: over the full-resolution plane — a focus underline one or two pixels thick can fall
			// between sampled rows, making two focus states look identical. HDMV: over the sampled pixels.
			var hash = 14695981039346656037UL;
			if (this._full is { } full)
			{
				foreach (var pixel in full)
				{
					hash = (hash ^ pixel) * 1099511628211UL;
				}

				return hash;
			}

			foreach (var pixel in this._plane)
			{
				hash = (hash ^ this.Resolve(pixel)) * 1099511628211UL;
			}

			return hash;
		}
	}

	/// <summary>A copy of the sampled plane for diffing.</summary>
	public uint[] Snapshot()
	{
		lock (this._gate)
		{
			return this._hidden ? new uint[this._plane.Length] : this._plane.Select(this.Resolve).ToArray();
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

	private static readonly bool _trace = Environment.GetEnvironmentVariable("DISC_MENU_TRACE") == "1";

	public void OnOverlay(BlurayNative.BdOverlay* ov)
	{
		if (_trace)
		{
			Console.Error.WriteLine($"[overlay] plane {ov->Plane} cmd {ov->Cmd} pal {ov->PaletteUpdateFlag} {ov->X},{ov->Y} {ov->W}x{ov->H} img {(ov->Img == IntPtr.Zero ? "-" : "y")}");
		}

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
					if (this._full is not null)
					{
						Array.Clear(this._full);
					}

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

	/// <summary>
	/// Saves the full-resolution BD-J graphics plane as a transparent PNG. False for HDMV menus (libvlc draws those
	/// itself) or when nothing is shown.
	/// </summary>
	public bool SaveArgbPlane(string path)
	{
		lock (this._gate)
		{
			if (this._full is null || this._hidden)
			{
				return false;
			}

			PngWriter.WriteArgb(path, this._full, this._fullWidth, this._fullHeight);
			return true;
		}
	}

	private void Init(int width, int height)
	{
		this._fullWidth = width;
		this._fullHeight = height;
		this._full = null;
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
		if (ov->Palette != IntPtr.Zero)
		{
			// Also the whole update when palette_update_flag is set (a highlight drawn by recolouring).
			new ReadOnlySpan<uint>((uint*)ov->Palette, 256).CopyTo(this._palette);
		}

		if (ov->Img == IntPtr.Zero || this._plane.Length == 0)
		{
			return;
		}

		this._indexed = true;

		// RLE elements: { uint16 len; uint16 color }. libbluray ends every row with an end-of-line element
		// { 0, 0 }; it must be consumed, or the next row reads it as an empty row and the rest of the image is
		// shifted (with sampled rows, whole objects then come out blank).
		var rle = (ushort*)ov->Img;
		for (var row = 0; row < ov->H; row++)
		{
			var py = ov->Y + row;
			for (var col = 0; ;)
			{
				if (col >= ov->W)
				{
					// Row complete: consume its end-of-line element when present.
					if (rle[0] == 0)
					{
						rle += 2;
					}

					break;
				}

				int length = rle[0];
				var color = rle[1];
				rle += 2;
				if (length == 0)
				{
					break;
				}

				if (py % Step == 0)
				{
					var value = (uint)(color & 0xff) + 1;
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
		if (this._fullWidth > 0 && this._fullHeight > 0)
		{
			this._full ??= new uint[this._fullWidth * this._fullHeight];
			for (var row = 0; row < ov->H && ov->Y + row < this._fullHeight; row++)
			{
				var count = Math.Min((int)ov->W, this._fullWidth - ov->X);
				if (count > 0)
				{
					new ReadOnlySpan<uint>(argb + (row * ov->Stride), count)
						.CopyTo(this._full.AsSpan(((ov->Y + row) * this._fullWidth) + ov->X, count));
				}
			}
		}

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
