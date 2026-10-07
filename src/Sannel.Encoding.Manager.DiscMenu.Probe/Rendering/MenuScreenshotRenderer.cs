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

	/// <summary>libvlc addresses (MRLs) for the disc, tried in order until one opens.</summary>
	private readonly List<string> _mrlCandidates = [];

	/// <summary>The address that opened the disc; reused for every later session.</summary>
	private string? _workingMrl;
	private string _discType = "dvd";
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
		// DISC_MENU_VLC_VERBOSE=1 lets libvlc log to stderr (diagnostics for discs whose menus do not render).
		var verbose = Environment.GetEnvironmentVariable("DISC_MENU_VLC_VERBOSE") == "1";
		this._libVlc = new LibVLC("--no-audio", "--no-video-title-show", "--no-osd", "--no-snapshot-preview", verbose ? "--verbose=2" : "--quiet", "--intf=dummy", "--no-xlib");
		this._lock = this.Lock;
		this._unlock = this.Unlock;
		this._display = this.Display;
	}

	/// <summary>Renders every menu that has a key path; failures are recorded on the menu, never thrown.</summary>
	public void Render(string input, string discType, DiscMenuMap map, string outputFolder)
	{
		this._discType = discType;
		var scheme = discType == "dvd" ? "dvd" : "bluray";
		var full = Path.GetFullPath(input).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		var forward = full.Replace('\\', '/');
		this._mrlCandidates.Clear();
		this._mrlCandidates.Add(scheme + new Uri(full).AbsoluteUri["file".Length..]); // dvd:///H:/a%20b (encoded)
		this._mrlCandidates.Add($"{scheme}://{(forward.StartsWith('/') ? string.Empty : "/")}{forward}"); // dvd:///H:/a b
		if (OperatingSystem.IsWindows())
		{
			this._mrlCandidates.Add($"{scheme}://{full}"); // dvd://H:\a b
		}


		foreach (var menu in map.Menus)
		{
			if (menu.Screenshot.Error is not null)
			{
				continue;
			}

			try
			{
				this.RenderMenu(discType, menu, outputFolder);
			}
			catch (Exception ex)
			{
				menu.Screenshot.Error = $"Rendering failed: {ex.Message}";
				this._log($"menu {menu.Id}: {ex}");
			}
		}
	}

	private void RenderMenu(string discType, MenuNode menu, string outputFolder)
	{
		if (discType != "dvd" && this.RenderComposited(discType, menu, outputFolder))
		{
			return;
		}

		var player = this.OpenAtMenu(discType, menu, out var failure);
		if (player is null)
		{
			menu.Screenshot.Error = failure;
			return;
		}

		var leftOver = new List<MenuButton>();
		try
		{
			menu.Screenshot = this.Capture(menu, null, outputFolder, menu.Id);
			if (menu.Screenshot.Available)
			{
				this._log($"menu {menu.Id}: screenshot saved");
			}

			// Walk the highlight from button to button in this one session using the neighbour links (starting a
			// Blu-ray Java menu again for every button would take far too long).
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
					if (discType == "dvd")
					{
						button.Screenshot = new MenuScreenshot { Error = "Not reachable with arrow keys from the previous button." };
					}
					else
					{
						leftOver.Add(button);
					}

					continue;
				}

				this.Press(player, keys);
				current = button.Number;
				button.Screenshot = this.Capture(menu, button.Number, outputFolder, $"{menu.Id}-b{button.Number}");
			}
		}
		finally
		{
			Close(player);
		}

		// Blu-ray buttons the neighbour links do not connect: replay each one's key path from the menu's start.
		foreach (var button in leftOver)
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

			var session = this.OpenAtMenu(discType, menu, out var sessionFailure);
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
	private MediaPlayer? OpenAtMenu(string discType, MenuNode menu, out string? failure)
	{
		var (width, height) = this.BufferSize(menu);
		this.AllocateBuffer(width * height * 4);

		// Until one address has worked, try each form: VLC's DVD/Blu-ray modules differ by platform in how they
		// parse the path (percent-encoding, a leading slash before a Windows drive letter).
		var candidates = this._workingMrl is { } known ? [known] : this._mrlCandidates;
		MediaPlayer? player = null;
		failure = null;
		var tried = new List<string>();
		foreach (var candidate in candidates)
		{
			player = new MediaPlayer(this._libVlc);
			player.SetVideoFormat("RV32", (uint)width, (uint)height, (uint)(width * 4));
			player.SetVideoCallbacks(this._lock, this._unlock, this._display);
			using (var media = new Media(this._libVlc, candidate, FromType.FromLocation))
			{
				Interlocked.Exchange(ref this._frames, 0);
				player.Play(media);
			}

			failure = this.WaitForFirstMenu(player, discType);
			if (failure is null)
			{
				if (this._workingMrl is null)
				{
					this._workingMrl = candidate;
					this._log($"libvlc opened the disc as {candidate}");
				}

				break;
			}

			// Only an instant "could not open" (no video at all) is worth trying another address for.
			var openFailed = Interlocked.Read(ref this._frames) == 0 && player.State is VLCState.Ended or VLCState.Error or VLCState.Stopped;
			tried.Add(candidate);
			Close(player);
			player = null;
			if (!openFailed)
			{
				break;
			}
		}

		if (player is null)
		{
			failure = tried.Count > 1 ? $"{failure} Addresses tried: {string.Join(" | ", tried)}" : failure;
			return null;
		}

		if (discType != "dvd" && menu.ReachPath is not [NavKey.TitleMenu or NavKey.RootMenu, ..])
		{
			// Let a Blu-ray menu's intro finish before pressing keys or capturing: BD-J menus show the background
			// first and slide the menu bar in a few seconds later.
			Thread.Sleep(_menuIntro);
		}

		this.Press(player, menu.ReachPath);
		return player;
	}

	/// <summary>How long a Blu-ray menu gets to finish its intro animation after it starts.</summary>
	private static readonly TimeSpan _menuIntro = TimeSpan.FromSeconds(8);

	private void Press(MediaPlayer player, IEnumerable<NavKey> keys)
	{
		foreach (var key in keys)
		{
			if (key is NavKey.TitleMenu or NavKey.RootMenu)
			{
				this.CallMenu(player);
				continue;
			}

			player.Navigate((uint)ToNavigationMode(key));
			Thread.Sleep(this._settle);
		}
	}

	/// <summary>
	/// The remote's Title Menu (Blu-ray) / Root Menu (DVD) key: libvlc has no navigate mode for it, but VLC's disc
	/// modules treat "set title 0" as that menu call. Waits for the menu to draw and finish its intro.
	/// </summary>
	private void CallMenu(MediaPlayer player)
	{
		player.Title = 0;
		Thread.Sleep(TimeSpan.FromSeconds(1));
		var clock = Stopwatch.StartNew();
		while (clock.Elapsed < TimeSpan.FromSeconds(30) && this.ContentFraction() < MinimumContentFraction)
		{
			Thread.Sleep(100);
		}

		Thread.Sleep(this._discType == "dvd" ? this._settle : _menuIntro);
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

		return this.SaveFrame(frame, menu, highlightedButton, outputFolder, name);
	}

	private MenuScreenshot SaveFrame(byte[] frame, MenuNode menu, int? highlightedButton, string outputFolder, string name)
	{
		var (width, height) = this.BufferSize(menu);
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

	/// <summary>The video buffer libvlc renders into: the menu's frame size.</summary>
	private (int Width, int Height) BufferSize(MenuNode menu) =>
		(menu.FrameWidth > 0 ? menu.FrameWidth : 720, menu.FrameHeight > 0 ? menu.FrameHeight : 480);

	/// <summary>
	/// BD-J menus: libvlc cannot blend BD-J graphics into its video ("no matching alpha blending routine (chroma:
	/// BGRA -> I420)"), so its frames show only the menu's background. The crawler saved each focus state's graphics
	/// plane; draw them over one background frame. Returns false when the overlays are not there.
	/// </summary>
	private bool RenderComposited(string discType, MenuNode menu, string outputFolder)
	{
		string OverlayPath(int state) => Path.Combine(outputFolder, $"{menu.Id}-s{state}-overlay.png");
		if (!File.Exists(OverlayPath(1)))
		{
			return false;
		}

		var player = this.OpenAtMenu(discType, menu, out var failure);
		if (player is null)
		{
			menu.Screenshot.Error = failure;
			return true;
		}

		byte[] background;
		try
		{
			Thread.Sleep(this._settle);
			lock (this._gate)
			{
				background = this._latest.ToArray();
			}
		}
		finally
		{
			Close(player);
		}

		var (width, height) = this.BufferSize(menu);
		menu.Screenshot = this.SaveFrame(OverlayBlender.Blend(background, width, height, OverlayPath(1)), menu, null, outputFolder, menu.Id);
		this._log($"menu {menu.Id}: screenshot saved (video + BD-J graphics)");
		foreach (var button in menu.Buttons)
		{
			button.Screenshot = File.Exists(OverlayPath(button.Number))
				? this.SaveFrame(OverlayBlender.Blend(background, width, height, OverlayPath(button.Number)), menu, button.Number, outputFolder, $"{menu.Id}-b{button.Number}")
				: new MenuScreenshot { Error = "The menu graphics for this button were not captured." };
		}

		return true;
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
