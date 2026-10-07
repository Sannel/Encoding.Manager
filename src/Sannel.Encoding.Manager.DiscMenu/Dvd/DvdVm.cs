using System.Runtime.InteropServices;
using Sannel.Encoding.Manager.DiscMenu.Native;

namespace Sannel.Encoding.Manager.DiscMenu.Dvd;

/// <summary>Navigation domain of the DVD virtual machine.</summary>
internal enum DvdDomain
{
	Unknown,
	FirstPlay,
	VideoManagerMenu,
	TitleSetMenu,
	Title,
}

/// <summary>A button read from the current PCI.</summary>
internal sealed record DvdButtonInfo(int Number, int X, int Y, int W, int H);

/// <summary>
/// Managed wrapper over one libdvdnav handle (the original or a <c>dvdnav_dup</c> snapshot).
/// libdvdnav runs the disc's navigation VM without decoding video, so stepping through menus is cheap.
/// </summary>
internal sealed unsafe class DvdVm : IDisposable
{
	private const int MaxButtons = 36;

	private readonly byte[] _buffer = new byte[DvdNavNative.BlockSize];
	private readonly bool _isDuplicate;
	private IntPtr _handle;

	private DvdVm(IntPtr handle, bool isDuplicate)
	{
		this._handle = handle;
		this._isDuplicate = isDuplicate;
	}

	/// <summary>Opens a disc folder (the folder containing VIDEO_TS, or VIDEO_TS itself).</summary>
	public static DvdVm Open(string path, string? menuLanguage)
	{
		if (DvdNavNative.dvdnav_open(out var handle, path) != DvdNavNative.StatusOk || handle == IntPtr.Zero)
		{
			throw new InvalidOperationException($"libdvdnav could not open '{path}'.");
		}

		var vm = new DvdVm(handle, false);
		DvdNavNative.dvdnav_set_readahead_flag(handle, 0);
		DvdNavNative.dvdnav_set_PGC_positioning_flag(handle, 1);
		if (!string.IsNullOrWhiteSpace(menuLanguage) && menuLanguage.Length >= 2)
		{
			DvdNavNative.dvdnav_menu_language_select(handle, menuLanguage[..2].ToLowerInvariant());
		}

		return vm;
	}

	/// <summary>Snapshot of the VM state; continues independently of this instance.</summary>
	/// <remarks>
	/// Only valid in a menu or title domain. When libdvdnav cannot copy the VM (no current PGC — before the VM
	/// starts or in First Play) its failure path frees IFO data shared with this handle, so it is never attempted.
	/// </remarks>
	public DvdVm Duplicate()
	{
		if (this.Domain is not (DvdDomain.VideoManagerMenu or DvdDomain.TitleSetMenu or DvdDomain.Title))
		{
			throw new InvalidOperationException("A DVD VM can only be duplicated in a menu or title domain.");
		}

		if (DvdNavNative.dvdnav_dup(out var dup, this._handle) != DvdNavNative.StatusOk || dup == IntPtr.Zero)
		{
			throw new InvalidOperationException("dvdnav_dup failed: " + this.LastError);
		}

		return new DvdVm(dup, true);
	}

	public string LastError
	{
		get
		{
			var ptr = DvdNavNative.dvdnav_err_to_string(this._handle);
			return ptr == IntPtr.Zero ? "unknown error" : Marshal.PtrToStringUTF8(ptr) ?? "unknown error";
		}
	}

	/// <summary>
	/// Reads the next block and returns its event. Still frames and waits are skipped automatically
	/// (the crawler never waits for real time). Returns -1 on a read error.
	/// </summary>
	public int Step()
	{
		int ev;
		int status;
		fixed (byte* buf = this._buffer)
		{
			status = DvdNavNative.dvdnav_get_next_block(this._handle, buf, out ev, out _);
		}

		if (status != DvdNavNative.StatusOk)
		{
			return -1;
		}

		switch (ev)
		{
			case DvdNavNative.EventStillFrame:
				DvdNavNative.dvdnav_still_skip(this._handle);
				break;
			case DvdNavNative.EventWait:
				DvdNavNative.dvdnav_wait_skip(this._handle);
				break;
		}

		return ev;
	}

	public DvdDomain Domain
	{
		get
		{
			if (DvdNavNative.dvdnav_is_domain_vts(this._handle) != 0)
			{
				return DvdDomain.Title;
			}

			if (DvdNavNative.dvdnav_is_domain_vtsm(this._handle) != 0)
			{
				return DvdDomain.TitleSetMenu;
			}

			if (DvdNavNative.dvdnav_is_domain_vmgm(this._handle) != 0)
			{
				return DvdDomain.VideoManagerMenu;
			}

			return DvdNavNative.dvdnav_is_domain_fp(this._handle) != 0 ? DvdDomain.FirstPlay : DvdDomain.Unknown;
		}
	}

	public bool IsMenuDomain => this.Domain is DvdDomain.VideoManagerMenu or DvdDomain.TitleSetMenu;

	/// <summary>In a title: (title, part). In a menu: (0, menu id) when the PGC is an entry menu.</summary>
	public (int Title, int Part) TitleInfo =>
		DvdNavNative.dvdnav_current_title_info(this._handle, out var title, out var part) == DvdNavNative.StatusOk
			? (title, part)
			: (-1, -1);

	public int CurrentButton =>
		DvdNavNative.dvdnav_get_current_highlight(this._handle, out var button) == DvdNavNative.StatusOk ? button : 0;

	public int AudioStream => DvdNavNative.dvdnav_get_active_audio_stream(this._handle);

	public int SpuStream => DvdNavNative.dvdnav_get_active_spu_stream(this._handle);

	public (int Width, int Height) VideoSize =>
		DvdNavNative.dvdnav_get_video_resolution(this._handle, out var w, out var h) == DvdNavNative.StatusOk && w > 0
			? ((int)w, (int)h)
			: (720, 480);

	/// <summary>Display aspect ratio of the current video (4:3 or 16:9).</summary>
	public double DisplayAspectRatio => DvdNavNative.dvdnav_get_video_aspect(this._handle) == 3 ? 16.0 / 9.0 : 4.0 / 3.0;

	/// <summary>Sector address of the current NAV pack (pci_gi.nv_pck_lbn).</summary>
	public uint NavPackLbn
	{
		get
		{
			var pci = DvdNavNative.dvdnav_get_current_nav_pci(this._handle);
			return pci == IntPtr.Zero ? 0 : *(uint*)(pci + DvdNavNative.PciNavPackLbnOffset);
		}
	}

	/// <summary>The buttons of the current PCI (empty when the PCI has no highlight information).</summary>
	public IReadOnlyList<DvdButtonInfo> ReadButtons()
	{
		var pci = DvdNavNative.dvdnav_get_current_nav_pci(this._handle);
		if (pci == IntPtr.Zero)
		{
			return [];
		}

		var buttons = new List<DvdButtonInfo>();
		for (var b = 1; b <= MaxButtons; b++)
		{
			if (DvdNavNative.dvdnav_get_highlight_area(pci, b, 0, out var area) != DvdNavNative.StatusOk)
			{
				break;
			}

			buttons.Add(new DvdButtonInfo(b, area.Sx, area.Sy, Math.Max(0, area.Ex - area.Sx), Math.Max(0, area.Ey - area.Sy)));
		}

		return buttons;
	}

	public bool SelectAndActivate(int button)
	{
		var pci = DvdNavNative.dvdnav_get_current_nav_pci(this._handle);
		return pci != IntPtr.Zero && DvdNavNative.dvdnav_button_select_and_activate(this._handle, pci, button) == DvdNavNative.StatusOk;
	}

	/// <summary>Which button an arrow key moves to from <paramref name="button"/>, probed on a throw-away snapshot.</summary>
	public int? Neighbour(int button, Model.NavKey direction)
	{
		using var probe = this.Duplicate();
		var pci = DvdNavNative.dvdnav_get_current_nav_pci(probe._handle);
		if (pci == IntPtr.Zero || DvdNavNative.dvdnav_button_select(probe._handle, pci, button) != DvdNavNative.StatusOk)
		{
			return null;
		}

		var status = direction switch
		{
			Model.NavKey.Up => DvdNavNative.dvdnav_upper_button_select(probe._handle, pci),
			Model.NavKey.Down => DvdNavNative.dvdnav_lower_button_select(probe._handle, pci),
			Model.NavKey.Left => DvdNavNative.dvdnav_left_button_select(probe._handle, pci),
			Model.NavKey.Right => DvdNavNative.dvdnav_right_button_select(probe._handle, pci),
			_ => 0,
		};
		if (status != DvdNavNative.StatusOk)
		{
			return null;
		}

		var target = probe.CurrentButton;
		return target > 0 && target != button ? target : null;
	}

	public bool MenuCall(int menuId) =>
		DvdNavNative.dvdnav_menu_call(this._handle, menuId) == DvdNavNative.StatusOk;

	/// <summary>
	/// Jumps forward inside the current cell using the DSI forward pointers, so long titles are crossed in a few
	/// reads while cell and post commands still run when the cell really ends. Returns false when already in the
	/// last VOBU of the cell (keep stepping normally).
	/// </summary>
	public bool SkipTowardCellEnd()
	{
		var dsi = DvdNavNative.dvdnav_get_current_nav_dsi(this._handle);
		if (dsi == IntPtr.Zero)
		{
			return false;
		}

		var forward = (uint*)(dsi + DvdNavNative.DsiForwardAddressOffset);
		for (var i = 0; i < DvdNavNative.DsiForwardAddressCount; i++)
		{
			var offset = forward[i] & DvdNavNative.SriEndOfCell;
			if (offset != 0 && offset != DvdNavNative.SriEndOfCell)
			{
				return DvdNavNative.dvdnav_sector_search(this._handle, offset, DvdNavNative.SeekCur) == DvdNavNative.StatusOk;
			}
		}

		return false;
	}

	public int TitleCount =>
		DvdNavNative.dvdnav_get_number_of_titles(this._handle, out var titles) == DvdNavNative.StatusOk ? titles : 0;

	public int PartCount(int title) =>
		DvdNavNative.dvdnav_get_number_of_parts(this._handle, title, out var parts) == DvdNavNative.StatusOk ? parts : 0;

	/// <summary>Title duration in seconds (0 when unknown).</summary>
	public int TitleDurationSeconds(int title)
	{
		// The chapter-times array is malloc'd by libdvdnav; the probe process is short-lived, so it is not freed
		// (freeing it from .NET would need the exact C runtime libdvdnav was built against).
		DvdNavNative.dvdnav_describe_title_chapters(this._handle, title, out _, out var duration);
		return (int)(duration / 90000);
	}

	public void Dispose()
	{
		if (this._handle == IntPtr.Zero)
		{
			return;
		}

		if (this._isDuplicate)
		{
			DvdNavNative.dvdnav_free_dup(this._handle);
		}
		else
		{
			DvdNavNative.dvdnav_close(this._handle);
		}

		this._handle = IntPtr.Zero;
	}
}
