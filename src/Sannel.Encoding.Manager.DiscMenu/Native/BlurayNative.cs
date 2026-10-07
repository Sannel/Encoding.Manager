using System.Runtime.InteropServices;

namespace Sannel.Encoding.Manager.DiscMenu.Native;

/// <summary>
/// Minimal P/Invoke surface of libbluray (bluray.h / overlay.h / keys.h). Structures are read at explicit
/// offsets that match the natural x64 C layout on both Linux and Windows.
/// </summary>
internal static unsafe partial class BlurayNative
{
	// bd_get_titles flags
	public const byte TitlesRelevant = 0x03;

	// BD_EVENT
	public const uint EventNone = 0;
	public const uint EventError = 1;
	public const uint EventEncrypted = 3;
	public const uint EventTitle = 5;
	public const uint EventPlaylist = 6;
	public const uint EventPlayItem = 7;
	public const uint EventChapter = 8;
	public const uint EventEndOfTitle = 10;
	public const uint EventAudioStream = 11;
	public const uint EventPgTextStStream = 13;
	public const uint EventPlaylistStop = 22;
	public const uint EventStill = 25;
	public const uint EventStillTime = 26;
	public const uint EventIdle = 28;
	public const uint EventPopup = 29;
	public const uint EventMenu = 30;

	// keys.h
	public const uint KeyRootMenu = 10;
	public const uint KeyPopup = 11;
	public const uint KeyUp = 12;
	public const uint KeyDown = 13;
	public const uint KeyLeft = 14;
	public const uint KeyRight = 15;
	public const uint KeyEnter = 16;

	// player settings
	public const uint SettingMenuLanguage = 18;
	public const uint SettingAudioLanguage = 16;
	public const uint SettingPgLanguage = 17;
	public const uint SettingPersistentStorage = 0x101;

	// overlay commands (BD_OVERLAY_* and BD_ARGB_OVERLAY_* share values)
	public const byte OverlayInit = 0;
	public const byte OverlayClose = 1;
	public const byte OverlayClear = 2;
	public const byte OverlayDraw = 3;
	public const byte OverlayWipe = 4;
	public const byte OverlayHide = 5;
	public const byte OverlayFlush = 6;

	/// <summary>Size of one aligned unit read by bd_read_ext.</summary>
	public const int AlignedUnit = 6144;

	[StructLayout(LayoutKind.Sequential)]
	public struct BdEvent
	{
		public uint Event;
		public uint Param;
	}

	/// <summary>BD_OVERLAY (HDMV PG/IG, palette RLE).</summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct BdOverlay
	{
		public long Pts;
		public byte Plane;
		public byte Cmd;
		public byte PaletteUpdateFlag;
		public ushort X;
		public ushort Y;
		public ushort W;
		public ushort H;
		public IntPtr Palette;
		public IntPtr Img;
	}

	/// <summary>BD_ARGB_OVERLAY (BD-J).</summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct BdArgbOverlay
	{
		public long Pts;
		public byte Plane;
		public byte Cmd;
		public ushort X;
		public ushort Y;
		public ushort W;
		public ushort H;
		public ushort Stride;
		public IntPtr Argb;
	}

	// BLURAY_DISC_INFO offsets (x64)
	public const int DiscInfoBlurayDetected = 0;
	public const int DiscInfoNoMenuSupport = 44;
	public const int DiscInfoFirstPlaySupported = 45;
	public const int DiscInfoTopMenuSupported = 46;
	public const int DiscInfoNumHdmvTitles = 80;
	public const int DiscInfoNumBdjTitles = 84;
	public const int DiscInfoBdjDetected = 92;
	public const int DiscInfoBdjSupported = 93;
	public const int DiscInfoLibJvmDetected = 94;
	public const int DiscInfoBdjHandled = 95;

	// BLURAY_TITLE_INFO offsets (x64)
	public const int TitleInfoPlaylist = 4;
	public const int TitleInfoDuration = 8;
	public const int TitleInfoChapterCount = 24;

	[LibraryImport(NativeLibraryResolver.Bluray, StringMarshalling = StringMarshalling.Utf8)]
	public static partial IntPtr bd_open(string devicePath, string? keyfilePath);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial void bd_close(IntPtr bd);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial IntPtr bd_get_disc_info(IntPtr bd);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial uint bd_get_titles(IntPtr bd, byte flags, uint minTitleLength);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial IntPtr bd_get_title_info(IntPtr bd, uint titleIndex, uint angle);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial IntPtr bd_get_playlist_info(IntPtr bd, uint playlist, uint angle);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial void bd_free_title_info(IntPtr titleInfo);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial int bd_set_player_setting(IntPtr bd, uint index, uint value);

	[LibraryImport(NativeLibraryResolver.Bluray, StringMarshalling = StringMarshalling.Utf8)]
	public static partial int bd_set_player_setting_str(IntPtr bd, uint index, string value);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial void bd_register_overlay_proc(IntPtr bd, IntPtr handle, delegate* unmanaged[Cdecl]<IntPtr, BdOverlay*, void> func);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial void bd_register_argb_overlay_proc(IntPtr bd, IntPtr handle, delegate* unmanaged[Cdecl]<IntPtr, BdArgbOverlay*, void> func, IntPtr buffer);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial int bd_play(IntPtr bd);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial int bd_menu_call(IntPtr bd, long pts);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial int bd_read_ext(IntPtr bd, byte* buffer, int length, out BdEvent ev);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial int bd_user_input(IntPtr bd, long pts, uint key);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial long bd_seek(IntPtr bd, ulong position);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial ulong bd_get_title_size(IntPtr bd);

	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial uint bd_get_current_chapter(IntPtr bd);

	/// <summary>Current playback position in 90 kHz ticks.</summary>
	[LibraryImport(NativeLibraryResolver.Bluray)]
	public static partial ulong bd_tell_time(IntPtr bd);
}
