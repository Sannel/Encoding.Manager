using System.Diagnostics;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using Sannel.Encoding.Manager.DiscMenu.Model;

namespace Sannel.Encoding.Manager.DiscMenu.Probe.Rendering;

/// <summary>
/// Renders each crawled menu with libvlc (headless, into a memory buffer) by playing the disc with menus
/// enabled and replaying the menu's key path, then saves raw and annotated PNGs.
/// </summary>
internal sealed class MenuScreenshotRenderer : IDisposable
{
	private static readonly TimeSpan _firstMenuTimeout = TimeSpan.FromSeconds(90);

	/// <summary>A Blu-ray frame counts as "showing something" once this share of it is not black.</summary>
	private const double MinimumContentFraction = 0.05;

	private readonly int _screenshotWidth;

	/// <summary>Per-button screenshots stop here so the probe finishes (and writes its map) before it is killed.</summary>
	private readonly DateTime _deadline;
	private readonly TimeSpan _settle;
	private readonly Action<string> _log;
	private readonly LibVLC _libVlc;
	private readonly object _gate = new();

	// Delegates are kept in fields so the GC never collects them while libvlc holds the function pointers.
	private readonly MediaPlayer.LibVLCVideoLockCb _lock;
	private readonly MediaPlayer.LibVLCVideoUnlockCb _unlock;
	private readonly MediaPlayer.LibVLCVideoDisplayCb _display;

	private IntPtr _buffer;
	private int _bufferBytes;
	private byte[] _latest = [];
	private long _frames;

	public MenuScreenshotRenderer(int screenshotWidth, TimeSpan settle, DateTime deadlineUtc, Action<string> log)
	{
		this._deadline = deadlineUtc;
		this._screenshotWidth = screenshotWidth;
		this._settle = settle;
		this._log = log;
		InstallLinuxResolver();
		Core.Initialize();
		this._libVlc = new LibVLC("--no-audio", "--no-video-title-show", "--no-osd", "--no-snapshot-preview", "--quiet", "--intf=dummy", "--no-xlib");
		this._lock = this.Lock;
		this._unlock = this.Unlock;
		this._display = this.Display;
	}

	/// <summary>Renders every menu that has a key path; failures are recorded on the menu, never thrown.</summary>
	public void Render(string input, string discType, DiscMenuMap map, string outputFolder)
	{
		var scheme = discType == "dvd" ? "dvd" : "bluray";
		var mrl = scheme + new Uri(Path.GetFullPath(input)).AbsoluteUri["file".Length..];

		foreach (var menu in map.Menus)
		{
			if (menu.Screenshot.Error is not null)
			{
				continue;
			}

			try
			{
				this.RenderMenu(mrl, discType, menu, outputFolder);
			}
			catch (Exception ex)
			{
				menu.Screenshot.Error = $"Rendering failed: {ex.Message}";
				this._log($"menu {menu.Id}: {ex}");
			}
		}
	}

	private void RenderMenu(string mrl, string discType, MenuNode menu, string outputFolder)
	{
		var player = this.OpenAtMenu(mrl, discType, menu, out var failure);
		if (player is null)
		{
			menu.Screenshot.Error = failure;
			return;
		}

		try
		{
			menu.Screenshot = this.Capture(menu, null, outputFolder, menu.Id);
			if (menu.Screenshot.Available)
			{
				this._log($"menu {menu.Id}: screenshot saved");
			}

			if (discType == "dvd")
			{
				// DVD: walk the highlight from button to button in this one session using the neighbour links.
				var current = menu.Buttons.FirstOrDefault(b => b.IsDefault)?.Number ?? menu.Buttons.FirstOrDefault()?.Number ?? 0;
				foreach (var button in menu.Buttons)
				{
					if (this.PastDeadline(button))
					{
						continue;
					}

					var keys = KeyPathPlanner.Plan(menu.Buttons, current, button.Number);
					if (keys is null)
					{
						button.Screenshot = new MenuScreenshot { Error = "Not reachable with arrow keys from the previous button." };
						continue;
					}

					this.Press(player, keys);
					current = button.Number;
					button.Screenshot = this.Capture(menu, button.Number, outputFolder, $"{menu.Id}-b{button.Number}");
				}
			}
		}
		finally
		{
			Close(player);
		}

		if (discType == "dvd")
		{
			return;
		}

		// Blu-ray: focus states are only known as key paths from the menu's start, so each gets a fresh session.
		foreach (var button in menu.Buttons)
		{
			if (this.PastDeadline(button))
			{
				continue;
			}

			if (button.FocusPath is null)
			{
				button.Screenshot = new MenuScreenshot { Error = "No key path to this button." };
				continue;
			}

			var session = this.OpenAtMenu(mrl, discType, menu, out var sessionFailure);
			if (session is null)
			{
				button.Screenshot = new MenuScreenshot { Error = sessionFailure };
				continue;
			}

			try
			{
				this.Press(session, button.FocusPath);
				button.Screenshot = this.Capture(menu, button.Number, outputFolder, $"{menu.Id}-b{button.Number}");
			}
			finally
			{
				Close(session);
			}
		}
	}

	/// <summary>Starts playback, waits for the first menu and replays the menu's key path. Null on failure.</summary>
	private MediaPlayer? OpenAtMenu(string mrl, string discType, MenuNode menu, out string? failure)
	{
		var width = menu.FrameWidth > 0 ? menu.FrameWidth : 720;
		var height = menu.FrameHeight > 0 ? menu.FrameHeight : 480;
		this.AllocateBuffer(width * height * 4);

		var player = new MediaPlayer(this._libVlc);
		player.SetVideoFormat("RV32", (uint)width, (uint)height, (uint)(width * 4));
		player.SetVideoCallbacks(this._lock, this._unlock, this._display);
		using (var media = new Media(this._libVlc, mrl, FromType.FromLocation))
		{
			Interlocked.Exchange(ref this._frames, 0);
			player.Play(media);
		}

		failure = this.WaitForFirstMenu(player, discType);
		if (failure is not null)
		{
			Close(player);
			return null;
		}

		if (discType != "dvd")
		{
			// Let a Blu-ray menu's intro finish before pressing keys or capturing: BD-J menus show the background
			// first and slide the menu bar in a few seconds later.
			Thread.Sleep(TimeSpan.FromSeconds(8));
		}

		this.Press(player, menu.ReachPath);
		return player;
	}

	private void Press(MediaPlayer player, IEnumerable<NavKey> keys)
	{
		foreach (var key in keys)
		{
			player.Navigate((uint)ToNavigationMode(key));
			Thread.Sleep(this._settle);
		}
	}

	/// <summary>Captures the current frame as {name}.png and {name}-annotated.png.</summary>
	private MenuScreenshot Capture(MenuNode menu, int? highlightedButton, string outputFolder, string name)
	{
		Thread.Sleep(this._settle);
		byte[] frame;
		lock (this._gate)
		{
			frame = this._latest.ToArray();
		}

		if (Interlocked.Read(ref this._frames) == 0 || frame.Length == 0)
		{
			return new MenuScreenshot { Error = "No video frame was rendered." };
		}

		var width = menu.FrameWidth > 0 ? menu.FrameWidth : 720;
		var height = menu.FrameHeight > 0 ? menu.FrameHeight : 480;
		var raw = $"{name}.png";
		var annotated = $"{name}-annotated.png";
		var (savedWidth, savedHeight) = ButtonAnnotator.Save(
			frame, width, height, menu, this._screenshotWidth,
			Path.Combine(outputFolder, raw), Path.Combine(outputFolder, annotated), highlightedButton);
		return new MenuScreenshot
		{
			Available = true,
			Width = savedWidth,
			Height = savedHeight,
			File = raw,
			AnnotatedFile = annotated,
		};
	}

	private bool PastDeadline(MenuButton button)
	{
		if (DateTime.UtcNow < this._deadline)
		{
			return false;
		}

		button.Screenshot = new MenuScreenshot { Error = "Skipped: the probe's time limit was reached." };
		return true;
	}

	private static void Close(MediaPlayer player)
	{
		player.Stop();
		player.Dispose();
	}

	/// <summary>
	/// Waits for the disc to settle on its first menu. For DVDs libvlc usually reports title 0 while in a menu domain;
	/// when it never does but video is playing, the menu is assumed to be up once the time limit passes (first-play
	/// warnings are much shorter). Returns null when ready, else a diagnostic message.
	/// </summary>
	private string? WaitForFirstMenu(MediaPlayer player, string discType)
	{
		var clock = Stopwatch.StartNew();
		var titlesSeen = new SortedSet<int>();
		while (clock.Elapsed < _firstMenuTimeout)
		{
			var hasFrames = Interlocked.Read(ref this._frames) > 0;
			var title = player.Title;
			titlesSeen.Add(title);
			// Blu-ray (especially BD-J) shows black frames while the Java menu starts; wait for real picture content.
			var atMenu = discType == "dvd" ? title == 0 : this.ContentFraction() >= MinimumContentFraction;
			if (hasFrames && atMenu)
			{
				Thread.Sleep(this._settle);
				return null;
			}

			if (player.State is VLCState.Error or VLCState.Ended or VLCState.Stopped && clock.Elapsed > TimeSpan.FromSeconds(5))
			{
				return $"libvlc stopped before a menu appeared (state {player.State}, {Interlocked.Read(ref this._frames)} frame(s), titles seen: {string.Join(", ", titlesSeen)}).";
			}

			Thread.Sleep(100);
		}

		var frames = Interlocked.Read(ref this._frames);
		if (frames > 0)
		{
			this._log($"no menu title reported after {_firstMenuTimeout.TotalSeconds:0}s (titles seen: {string.Join(", ", titlesSeen)}); using the current picture");
			return null;
		}

		return $"libvlc rendered no video within {_firstMenuTimeout.TotalSeconds:0}s (state {player.State}, titles seen: {string.Join(", ", titlesSeen)}). Check that disc-menu-probe\\libvlc\\win-x64\\plugins exists.";
	}

	/// <summary>
	/// Distros ship only the versioned runtime libraries (libvlc.so.5, libvlccore.so.9); the unversioned names
	/// LibVLCSharp imports exist only with the -dev packages.
	/// </summary>
	private static void InstallLinuxResolver()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}

		try
		{
			NativeLibrary.SetDllImportResolver(typeof(LibVLC).Assembly, (name, _, _) =>
			{
				string[] candidates = name switch
				{
					"libvlc" => ["libvlc.so.5", "libvlc.so"],
					"libvlccore" => ["libvlccore.so.9", "libvlccore.so"],
					_ => [],
				};
				foreach (var candidate in candidates)
				{
					if (NativeLibrary.TryLoad(candidate, out var handle))
					{
						return handle;
					}
				}

				return IntPtr.Zero;
			});
		}
		catch (InvalidOperationException)
		{
			// A resolver is already installed for LibVLCSharp; use it.
		}
	}

	private static NavigationMode ToNavigationMode(NavKey key) => key switch
	{
		NavKey.Up => NavigationMode.Up,
		NavKey.Down => NavigationMode.Down,
		NavKey.Left => NavigationMode.Left,
		NavKey.Right => NavigationMode.Right,
		NavKey.Enter => NavigationMode.Activate,
		_ => NavigationMode.Popup,
	};

	private void AllocateBuffer(int bytes)
	{
		lock (this._gate)
		{
			if (this._bufferBytes == bytes)
			{
				return;
			}

			if (this._buffer != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(this._buffer);
			}

			this._buffer = Marshal.AllocHGlobal(bytes);
			this._bufferBytes = bytes;
			this._latest = new byte[bytes];
		}
	}

	/// <summary>Share of sampled pixels that are not near-black in the latest frame (0 when there is no frame).</summary>
	private double ContentFraction()
	{
		lock (this._gate)
		{
			if (this._latest.Length < 4 || Interlocked.Read(ref this._frames) == 0)
			{
				return 0;
			}

			var sampled = 0;
			var lit = 0;
			for (var i = 0; i + 3 < this._latest.Length; i += 4 * 97)
			{
				sampled++;
				// BGRA: a pixel counts as content when any channel is clearly above black.
				if (this._latest[i] > 24 || this._latest[i + 1] > 24 || this._latest[i + 2] > 24)
				{
					lit++;
				}
			}

			return sampled == 0 ? 0 : (double)lit / sampled;
		}
	}

	private IntPtr Lock(IntPtr opaque, IntPtr planes)
	{
		Marshal.WriteIntPtr(planes, this._buffer);
		return IntPtr.Zero;
	}

	private void Unlock(IntPtr opaque, IntPtr picture, IntPtr planes)
	{
	}

	private void Display(IntPtr opaque, IntPtr picture)
	{
		lock (this._gate)
		{
			Marshal.Copy(this._buffer, this._latest, 0, this._bufferBytes);
		}

		Interlocked.Increment(ref this._frames);
	}

	public void Dispose()
	{
		this._libVlc.Dispose();
		if (this._buffer != IntPtr.Zero)
		{
			Marshal.FreeHGlobal(this._buffer);
			this._buffer = IntPtr.Zero;
		}
	}
}
