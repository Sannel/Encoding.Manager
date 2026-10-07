using System.Runtime.InteropServices;

namespace Sannel.Encoding.Manager.DiscMenu.Native;

/// <summary>
/// Minimal P/Invoke surface of libdvdnav (dvdnav.h, libdvdnav 6.1+ for <c>dvdnav_dup</c>).
/// Only the public API is used; pci_t / dsi_t are read at fixed packed offsets that contain no bitfields.
/// </summary>
internal static unsafe partial class DvdNavNative
{
	public const int StatusOk = 1;

	// dvdnav_events.h
	public const int EventBlockOk = 0;
	public const int EventNop = 1;
	public const int EventStillFrame = 2;
	public const int EventSpuStreamChange = 3;
	public const int EventAudioStreamChange = 4;
	public const int EventVtsChange = 5;
	public const int EventCellChange = 6;
	public const int EventNavPacket = 7;
	public const int EventStop = 8;
	public const int EventHighlight = 9;
	public const int EventSpuClutChange = 10;
	public const int EventHopChannel = 12;
	public const int EventWait = 13;

	// DVDMenuID_t
	public const int MenuEscape = 0;
	public const int MenuTitle = 2;
	public const int MenuRoot = 3;
	public const int MenuSubpicture = 4;
	public const int MenuAudio = 5;
	public const int MenuAngle = 6;
	public const int MenuPart = 7;

	public const int SeekSet = 0;
	public const int SeekCur = 1;

	public const int BlockSize = 2048;

	/// <summary>Offset of <c>pci_gi.nv_pck_lbn</c> in pci_t.</summary>
	public const int PciNavPackLbnOffset = 0;

	/// <summary>Offset of <c>vobu_sri.fwda[0]</c> in dsi_t (dsi_gi 32 + sml_pbi 148 + sml_agli 54 + next_video 4).</summary>
	public const int DsiForwardAddressOffset = 238;

	/// <summary>Number of forward VOBU pointers in <c>vobu_sri.fwda</c>.</summary>
	public const int DsiForwardAddressCount = 19;

	/// <summary>Offset of <c>vobu_sri.next_vobu</c> in dsi_t.</summary>
	public const int DsiNextVobuOffset = 314;

	/// <summary>SRI_END_OF_CELL.</summary>
	public const uint SriEndOfCell = 0x3fffffff;

	[StructLayout(LayoutKind.Sequential)]
	public struct HighlightArea
	{
		public uint Palette;
		public ushort Sx;
		public ushort Sy;
		public ushort Ex;
		public ushort Ey;
		public uint Pts;
		public uint ButtonN;
	}

	[LibraryImport(NativeLibraryResolver.DvdNav, StringMarshalling = StringMarshalling.Utf8)]
	public static partial int dvdnav_open(out IntPtr dest, string path);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_close(IntPtr self);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_dup(out IntPtr dest, IntPtr src);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_free_dup(IntPtr self);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial IntPtr dvdnav_err_to_string(IntPtr self);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_set_readahead_flag(IntPtr self, int flag);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_set_PGC_positioning_flag(IntPtr self, int flag);

	[LibraryImport(NativeLibraryResolver.DvdNav, StringMarshalling = StringMarshalling.Utf8)]
	public static partial int dvdnav_menu_language_select(IntPtr self, string code);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_get_next_block(IntPtr self, byte* buf, out int @event, out int len);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_still_skip(IntPtr self);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_wait_skip(IntPtr self);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_get_number_of_titles(IntPtr self, out int titles);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_get_number_of_parts(IntPtr self, int title, out int parts);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial uint dvdnav_describe_title_chapters(IntPtr self, int title, out IntPtr times, out ulong duration);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_menu_call(IntPtr self, int menu);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_current_title_info(IntPtr self, out int title, out int part);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_sector_search(IntPtr self, long offset, int origin);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_get_current_highlight(IntPtr self, out int button);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial IntPtr dvdnav_get_current_nav_pci(IntPtr self);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial IntPtr dvdnav_get_current_nav_dsi(IntPtr self);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_get_highlight_area(IntPtr pci, int button, int mode, out HighlightArea highlight);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_upper_button_select(IntPtr self, IntPtr pci);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_lower_button_select(IntPtr self, IntPtr pci);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_left_button_select(IntPtr self, IntPtr pci);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_right_button_select(IntPtr self, IntPtr pci);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_button_select(IntPtr self, IntPtr pci, int button);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_button_select_and_activate(IntPtr self, IntPtr pci, int button);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial sbyte dvdnav_is_domain_fp(IntPtr self);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial sbyte dvdnav_is_domain_vmgm(IntPtr self);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial sbyte dvdnav_is_domain_vtsm(IntPtr self);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial sbyte dvdnav_is_domain_vts(IntPtr self);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial sbyte dvdnav_get_active_audio_stream(IntPtr self);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial sbyte dvdnav_get_active_spu_stream(IntPtr self);

	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial int dvdnav_get_video_resolution(IntPtr self, out uint width, out uint height);

	/// <summary>0 = 4:3, 3 = 16:9.</summary>
	[LibraryImport(NativeLibraryResolver.DvdNav)]
	public static partial byte dvdnav_get_video_aspect(IntPtr self);
}
