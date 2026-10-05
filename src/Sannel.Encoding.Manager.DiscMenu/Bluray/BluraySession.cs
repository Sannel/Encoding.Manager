using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Sannel.Encoding.Manager.DiscMenu.Native;

namespace Sannel.Encoding.Manager.DiscMenu.Bluray;

/// <summary>Static facts about a Blu-ray disc from <c>bd_get_disc_info</c>.</summary>
internal sealed record BlurayDiscInfo(
	bool Detected,
	bool NoMenuSupport,
	bool FirstPlaySupported,
	bool TopMenuSupported,
	int HdmvTitles,
	int BdjTitles,
	bool BdjDetected,
	bool BdjHandled,
	bool LibJvmDetected);

/// <summary>A playlist from <c>bd_get_title_info</c>.</summary>
internal sealed record BlurayPlaylistInfo(int Playlist, int DurationSeconds, int ChapterCount);

/// <summary>
/// One libbluray playback session with menus enabled. libbluray has no state snapshot, so the crawler opens a
/// fresh session and replays key presses whenever it needs to get back to a menu state.
/// </summary>
internal sealed unsafe class BluraySession : IDisposable
{
	private readonly byte[] _buffer = new byte[BlurayNative.AlignedUnit * 32];
	private GCHandle _self;
	private IntPtr _bd;

	private BluraySession(IntPtr bd)
	{
		this._bd = bd;
		this._self = GCHandle.Alloc(this);
	}

	public OverlayCompositor Overlay { get; } = new();

	public int? Playlist { get; private set; }

	public int? Chapter { get; private set; }

	public bool MenuActive { get; private set; }

	public bool PopupAvailable { get; private set; }

	public int EndOfTitleCount { get; private set; }

	public bool Failed { get; private set; }

	/// <summary>Why playback failed, when <see cref="Failed"/> is true.</summary>
	public string? FailureReason { get; private set; }

	/// <summary>Number of BD_EVENT_IDLE events seen (BD-J title running but no playlist yet).</summary>
	public int IdleEvents { get; private set; }

	public int? AudioStream { get; private set; }

	public int? SubtitleStream { get; private set; }

	public static BluraySession Open(string path, string menuLanguage)
	{
		var bd = BlurayNative.bd_open(path, null);
		if (bd == IntPtr.Zero)
		{
			throw new InvalidOperationException($"libbluray could not open '{path}'.");
		}

		var session = new BluraySession(bd);
		var language = ToIso6392(menuLanguage);
		BlurayNative.bd_set_player_setting_str(bd, BlurayNative.SettingMenuLanguage, language);
		BlurayNative.bd_set_player_setting_str(bd, BlurayNative.SettingAudioLanguage, language);
		BlurayNative.bd_set_player_setting_str(bd, BlurayNative.SettingPgLanguage, language);
		BlurayNative.bd_set_player_setting(bd, BlurayNative.SettingPersistentStorage, 0);
		var handle = GCHandle.ToIntPtr(session._self);
		BlurayNative.bd_register_overlay_proc(bd, handle, &OnOverlay);
		BlurayNative.bd_register_argb_overlay_proc(bd, handle, &OnArgbOverlay, IntPtr.Zero);
		return session;
	}

	public BlurayDiscInfo ReadDiscInfo()
	{
		var info = BlurayNative.bd_get_disc_info(this._bd);
		if (info == IntPtr.Zero)
		{
			return new BlurayDiscInfo(false, true, false, false, 0, 0, false, false, false);
		}

		var b = (byte*)info;
		return new BlurayDiscInfo(
			b[BlurayNative.DiscInfoBlurayDetected] != 0,
			b[BlurayNative.DiscInfoNoMenuSupport] != 0,
			b[BlurayNative.DiscInfoFirstPlaySupported] != 0,
			b[BlurayNative.DiscInfoTopMenuSupported] != 0,
			*(int*)(b + BlurayNative.DiscInfoNumHdmvTitles),
			*(int*)(b + BlurayNative.DiscInfoNumBdjTitles),
			b[BlurayNative.DiscInfoBdjDetected] != 0,
			b[BlurayNative.DiscInfoBdjHandled] != 0,
			b[BlurayNative.DiscInfoLibJvmDetected] != 0);
	}

	/// <summary>Playlists libbluray considers relevant (duplicates filtered), at least <paramref name="minSeconds"/> long.</summary>
	public List<BlurayPlaylistInfo> ReadPlaylists(uint minSeconds)
	{
		var result = new List<BlurayPlaylistInfo>();
		var count = BlurayNative.bd_get_titles(this._bd, BlurayNative.TitlesRelevant, minSeconds);
		for (uint i = 0; i < count; i++)
		{
			var info = BlurayNative.bd_get_title_info(this._bd, i, 0);
			if (info != IntPtr.Zero)
			{
				result.Add(ToPlaylistInfo(info));
				BlurayNative.bd_free_title_info(info);
			}
		}

		return result;
	}

	public BlurayPlaylistInfo? ReadPlaylist(int playlist)
	{
		var info = BlurayNative.bd_get_playlist_info(this._bd, (uint)playlist, 0);
		if (info == IntPtr.Zero)
		{
			return null;
		}

		var result = ToPlaylistInfo(info);
		BlurayNative.bd_free_title_info(info);
		return result;
	}

	public bool Play()
	{
		if (BlurayNative.bd_play(this._bd) != 0)
		{
			return true;
		}

		this.Fail("bd_play failed: libbluray could not start first play / the top menu.");
		return false;
	}

	private void Fail(string reason)
	{
		this.Failed = true;
		this.FailureReason ??= reason;
	}

	public bool TopMenu() => BlurayNative.bd_menu_call(this._bd, -1) != 0;

	public void Press(uint key) => BlurayNative.bd_user_input(this._bd, -1, key);

	/// <summary>Seeks close to the end of the current playlist so its end-of-playlist navigation runs.</summary>
	public void SeekNearEnd()
	{
		var size = BlurayNative.bd_get_title_size(this._bd);
		var back = (ulong)BlurayNative.AlignedUnit * 256;
		if (size > back)
		{
			BlurayNative.bd_seek(this._bd, size - back);
		}
	}

	/// <summary>
	/// Reads (and handles events) until <paramref name="until"/> is true, the plane stops changing for
	/// <paramref name="quietReads"/> reads and <paramref name="quietTime"/>, or a limit is hit.
	/// </summary>
	public void Pump(Func<bool>? until, int quietReads, TimeSpan quietTime, int maxReads, TimeSpan maxTime)
	{
		var clock = Stopwatch.StartNew();
		var lastFlush = this.Overlay.FlushCount;
		var lastChangeRead = 0;
		var lastChangeTime = TimeSpan.Zero;
		for (var reads = 0; reads < maxReads && clock.Elapsed < maxTime && !this.Failed; reads++)
		{
			int read;
			BlurayNative.BdEvent ev;
			fixed (byte* buf = this._buffer)
			{
				read = BlurayNative.bd_read_ext(this._bd, buf, this._buffer.Length, out ev);
			}

			if (read < 0)
			{
				this.Fail("libbluray returned a read error.");
				break;
			}

			this.Handle(ev);
			if (read == 0 && ev.Event is BlurayNative.EventNone or BlurayNative.EventIdle)
			{
				// Still frame / BD-J idle: libbluray answers instantly, so without a pause a read-count limit is used
				// up in milliseconds and the Java menu never gets time to react. Pausing keeps every wait time-based.
				Thread.Sleep(10);
			}

			if (until?.Invoke() == true)
			{
				return;
			}

			var flush = this.Overlay.FlushCount;
			if (flush != lastFlush)
			{
				lastFlush = flush;
				lastChangeRead = reads;
				lastChangeTime = clock.Elapsed;
			}
			else if (until is null && reads - lastChangeRead >= quietReads && clock.Elapsed - lastChangeTime >= quietTime)
			{
				return;
			}
		}
	}

	private void Handle(BlurayNative.BdEvent ev)
	{
		switch (ev.Event)
		{
			case BlurayNative.EventError:
				// BD_ERROR_HDMV = 1, BD_ERROR_BDJ = 2 (the Java VM or the disc's Xlet failed to start).
				this.Fail(ev.Param switch
				{
					1 => "libbluray reported a fatal HDMV navigation error.",
					2 => "libbluray could not start the BD-J (Java) menu: the JVM or the disc's Java application failed to start. Check DiscMenu:JavaHome (Java 17/21, 64-bit) and the libbluray-j2se jar version.",
					_ => $"libbluray reported a fatal error (code {ev.Param}).",
				});
				break;
			case BlurayNative.EventEncrypted:
				this.Fail("The disc is encrypted (AACS/BD+); libbluray cannot play it.");
				break;
			case BlurayNative.EventPlaylist:
				this.Playlist = (int)ev.Param;
				this.Chapter = null;
				break;
			case BlurayNative.EventChapter:
				this.Chapter = (int)ev.Param;
				break;
			case BlurayNative.EventMenu:
				this.MenuActive = ev.Param != 0;
				break;
			case BlurayNative.EventPopup:
				this.PopupAvailable = ev.Param != 0;
				break;
			case BlurayNative.EventEndOfTitle:
			case BlurayNative.EventPlaylistStop:
				this.EndOfTitleCount++;
				break;
			case BlurayNative.EventIdle:
				this.IdleEvents++;
				break;
			case BlurayNative.EventAudioStream:
				this.AudioStream = (int)ev.Param;
				break;
			case BlurayNative.EventPgTextStStream:
				this.SubtitleStream = (int)ev.Param;
				break;
		}
	}

	private static BlurayPlaylistInfo ToPlaylistInfo(IntPtr info)
	{
		var b = (byte*)info;
		return new BlurayPlaylistInfo(
			*(int*)(b + BlurayNative.TitleInfoPlaylist),
			(int)(*(ulong*)(b + BlurayNative.TitleInfoDuration) / 90000),
			*(int*)(b + BlurayNative.TitleInfoChapterCount));
	}

	private static string ToIso6392(string language) => language.ToLowerInvariant() switch
	{
		{ Length: 3 } three => three,
		"en" => "eng",
		"fr" => "fre",
		"de" => "ger",
		"es" => "spa",
		"it" => "ita",
		"ja" => "jpn",
		"nl" => "dut",
		"pt" => "por",
		"sv" => "swe",
		"zh" => "chi",
		"ko" => "kor",
		_ => "eng",
	};

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	private static void OnOverlay(IntPtr handle, BlurayNative.BdOverlay* ov)
	{
		if (ov != null && GCHandle.FromIntPtr(handle).Target is BluraySession session)
		{
			session.Overlay.OnOverlay(ov);
		}
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	private static void OnArgbOverlay(IntPtr handle, BlurayNative.BdArgbOverlay* ov)
	{
		if (ov != null && GCHandle.FromIntPtr(handle).Target is BluraySession session)
		{
			session.Overlay.OnArgbOverlay(ov);
		}
	}

	public void Dispose()
	{
		if (this._bd != IntPtr.Zero)
		{
			BlurayNative.bd_close(this._bd);
			this._bd = IntPtr.Zero;
		}

		if (this._self.IsAllocated)
		{
			this._self.Free();
		}
	}
}
