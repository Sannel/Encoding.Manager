using System.Diagnostics;
using Sannel.Encoding.Manager.DiscMenu.Model;
using Sannel.Encoding.Manager.DiscMenu.Native;

namespace Sannel.Encoding.Manager.DiscMenu.Bluray;

/// <summary>
/// Maps Blu-ray menus (HDMV, and BD-J when a JRE is available) from the outside: libbluray exposes no button
/// table, so the crawler presses arrow keys, fingerprints the interactive-graphics plane to discover focus states
/// (= buttons), infers each button's rectangle from the pixels that change, then presses Enter on each button in a
/// fresh session and watches the playlist / chapter / menu events that follow.
/// </summary>
public sealed class BlurayMenuCrawler
{
	private const int MaxButtonsPerMenu = 36;
	private static readonly NavKey[] _directions = [NavKey.Down, NavKey.Right, NavKey.Up, NavKey.Left];

	private readonly CrawlOptions _options;
	private readonly Action<string> _log;
	private readonly List<MenuInfo> _menus = [];
	private readonly Dictionary<ulong, MenuInfo> _menusByFingerprint = [];
	private DiscMenuMap _map = new();
	private Stopwatch _clock = new();
	private string _path = string.Empty;
	private bool _bdj;
	private string? _reachFailure;
	private int _actions;

	public BlurayMenuCrawler(CrawlOptions options, Action<string>? log = null)
	{
		this._options = options;
		this._log = log ?? (_ => { });
	}

	/// <summary>Crawls the Blu-ray folder at <paramref name="path"/> (the folder containing BDMV).</summary>
	public DiscMenuMap Crawl(string path)
	{
		this._clock = Stopwatch.StartNew();
		this._path = path;
		this._map = new DiscMenuMap { DiscType = "BluRay" };

		BlurayDiscInfo info;
		using (var session = BluraySession.Open(path, this._options.MenuLanguage))
		{
			info = session.ReadDiscInfo();
			foreach (var playlist in session.ReadPlaylists(60))
			{
				this._map.Titles.Add(new DiscTitle
				{
					Playlist = playlist.Playlist,
					ChapterCount = playlist.ChapterCount,
					DurationSeconds = playlist.DurationSeconds,
				});
			}
		}

		this._bdj = info.BdjDetected;
		this._map.MenuSystem = info.BdjDetected ? "BD-J" : "HDMV";

		if (info.NoMenuSupport || (!info.FirstPlaySupported && !info.TopMenuSupported))
		{
			this._map.MenuSystem = "None";
			this._map.Warnings.Add("This disc has no menus that libbluray can run; only the playlist list is available.");
			return this.Finish();
		}

		if (info.BdjDetected && !info.BdjHandled)
		{
			this._map.Complete = false;
			this._map.Warnings.Add(info.LibJvmDetected
				? "BD-J (Java) menu detected, but libbluray could not start it (is the libbluray-j2se jar available? DiscMenu:LibBlurayJarPath)."
				: "BD-J (Java) menu detected, but no Java runtime is configured (DiscMenu:JavaHome). Only the playlist list is available.");
			return this.Finish();
		}

		this._map.Warnings.Add("Blu-ray menus are mapped by pressing keys and watching playback events; button areas and chapter ranges are best-effort.");

		var first = this.DiscoverMenu([], null);
		if (first is null)
		{
			this._map.Complete = false;
			this._map.Warnings.Add("No menu appeared after first play or the top-menu key: " + (this._reachFailure ?? "unknown reason."));
			return this.Finish();
		}

		for (var i = 0; i < this._menus.Count; i++)
		{
			var menu = this._menus[i];
			for (var b = 0; b < menu.States.Count; b++)
			{
				if (!this.HasBudget())
				{
					return this.Finish();
				}

				this._actions++;
				menu.Node.Buttons[b].Action = this.Activate(menu, menu.States[b]);
			}
		}

		return this.Finish();
	}

	private DiscMenuMap Finish()
	{
		foreach (var menu in this._map.Menus)
		{
			foreach (var button in menu.Buttons.Where(b => b.Action.Type == ButtonActionType.PlayTitle))
			{
				this._map.Titles.FirstOrDefault(t => t.Playlist == button.Action.Playlist)
					?.ReachedFromButtons.Add($"{menu.Id}#{button.Number}");
			}
		}

		this._map.CrawlMilliseconds = (int)this._clock.ElapsedMilliseconds;
		return this._map;
	}

	/// <summary>Opens a session and plays until a menu is showing, then replays <paramref name="keys"/>.</summary>
	private BluraySession? Reach(List<NavKey> keys, out bool usedTopMenu)
	{
		usedTopMenu = false;
		var session = BluraySession.Open(this._path, this._options.MenuLanguage);
		if (!session.Play())
		{
			this._reachFailure = session.FailureReason;
			session.Dispose();
			return null;
		}

		// BD-J (Java) needs time to start its JVM and Xlet before anything is drawn.
		var wait = this.Remaining(TimeSpan.FromSeconds(this._bdj ? 60 : 30));
		session.Pump(() => this.IsMenuShowing(session), 0, TimeSpan.Zero, 40_000, wait);
		if (!this.IsMenuShowing(session) && !session.Failed)
		{
			// Trailers / an auto-playing feature: jump to the top menu.
			session.TopMenu();
			usedTopMenu = true;
			session.Pump(() => this.IsMenuShowing(session), 0, TimeSpan.Zero, 40_000, this.Remaining(TimeSpan.FromSeconds(30)));
		}

		if (!this.IsMenuShowing(session))
		{
			this._reachFailure = session.FailureReason
				?? $"No menu graphics appeared (playlist {session.Playlist?.ToString() ?? "none"}, menu event {(session.MenuActive ? "on" : "off")}, {session.IdleEvents} idle event(s), overlay {(session.Overlay.FlushCount > 0 ? $"drew {session.Overlay.FlushCount} frame(s)" : "never drew")}).";
			session.Dispose();
			return null;
		}

		this.Settle(session);
		foreach (var key in keys)
		{
			this.Press(session, key);
		}

		return session;
	}

	/// <summary>
	/// HDMV menus raise BD_EVENT_MENU; BD-J menus never do, so for them a visible interactive-graphics overlay is
	/// taken as "a menu is showing".
	/// </summary>
	private bool IsMenuShowing(BluraySession session) =>
		session.Overlay.IsVisible && (session.MenuActive || this._bdj);

	private void Press(BluraySession session, NavKey key)
	{
		switch (key)
		{
			case NavKey.TitleMenu:
				session.TopMenu();
				break;
			case NavKey.PopUp:
			case NavKey.RootMenu:
				session.Press(BlurayNative.KeyPopup);
				break;
			default:
				session.Press(key switch
				{
					NavKey.Up => BlurayNative.KeyUp,
					NavKey.Down => BlurayNative.KeyDown,
					NavKey.Left => BlurayNative.KeyLeft,
					NavKey.Right => BlurayNative.KeyRight,
					_ => BlurayNative.KeyEnter,
				});
				break;
		}

		this.Settle(session);
	}

	private void Settle(BluraySession session) =>
		session.Pump(null, 24, this._bdj ? TimeSpan.FromMilliseconds(600) : TimeSpan.Zero, 4_000, this.Remaining(TimeSpan.FromSeconds(10)));

	/// <summary>Reaches a menu, walks its focus states with the arrow keys and records it as a node.</summary>
	private MenuInfo? DiscoverMenu(List<NavKey> path, MenuInfo? parent)
	{
		using var session = this.Reach(path, out var usedTopMenu);
		if (session is null)
		{
			return null;
		}

		var reachPath = usedTopMenu && path.Count == 0 ? [NavKey.TitleMenu] : path;
		var fingerprint = session.Overlay.Fingerprint();
		if (this._menusByFingerprint.TryGetValue(fingerprint, out var known))
		{
			return known;
		}

		if (this._menus.Count >= this._options.MaxMenus)
		{
			this.MarkIncomplete($"Menu limit ({this._options.MaxMenus}) reached; some menus were not explored.");
			return null;
		}

		// Walk focus states in this one session: from the current state try each arrow key, moving back to
		// known states through edges already discovered.
		var states = new List<FocusState> { new(fingerprint, [], session.Overlay.Snapshot()) };
		var edges = new Dictionary<(int From, NavKey Key), int>();
		var current = 0;
		var pending = new Queue<(int State, NavKey Key)>(_directions.Select(d => (0, d)));
		while (pending.Count > 0 && states.Count < MaxButtonsPerMenu && this._clock.Elapsed < this._options.TimeBudget)
		{
			var (from, key) = pending.Dequeue();
			var route = Route(edges, current, from);
			if (route is null)
			{
				continue;
			}

			foreach (var step in route)
			{
				this.Press(session, step);
			}

			this.Press(session, key);
			var now = session.Overlay.Fingerprint();
			if (!session.MenuActive && !session.Overlay.IsVisible)
			{
				// The key left the menu (e.g. an auto-action button); stop walking this session.
				break;
			}

			var index = states.FindIndex(s => s.Fingerprint == now);
			if (index < 0)
			{
				states.Add(new FocusState(now, [.. states[from].Keys, key], session.Overlay.Snapshot()));
				index = states.Count - 1;
				foreach (var d in _directions)
				{
					pending.Enqueue((index, d));
				}
			}

			edges[(from, key)] = index;
			current = index;
		}

		var (sampledWidth, sampledHeight) = session.Overlay.SampledSize;
		var rects = InferRects(states, sampledWidth, sampledHeight);
		var node = new MenuNode
		{
			Id = $"m{this._menus.Count}",
			Kind = session.PopupAvailable && !session.MenuActive ? MenuKind.PopUp : (this._menus.Count == 0 ? MenuKind.Title : MenuKind.Other),
			Domain = this._bdj ? "BD-J" : "HDMV",
			ReachPath = reachPath,
			ParentMenuId = parent?.Node.Id,
			FrameWidth = session.Overlay.Width > 0 ? session.Overlay.Width : 1920,
			FrameHeight = session.Overlay.Height > 0 ? session.Overlay.Height : 1080,
			DisplayAspectRatio = 1.778,
			Buttons = states.Select((s, i) => new MenuButton
			{
				Number = i + 1,
				IsDefault = i == 0,
				Rect = rects[i],
				Neighbours = new ButtonNeighbours
				{
					Up = Neighbour(edges, i, NavKey.Up),
					Down = Neighbour(edges, i, NavKey.Down),
					Left = Neighbour(edges, i, NavKey.Left),
					Right = Neighbour(edges, i, NavKey.Right),
				},
			}).ToList(),
		};

		var menu = new MenuInfo(node, reachPath, states);
		this._menus.Add(menu);
		this._menusByFingerprint[fingerprint] = menu;
		this._map.Menus.Add(node);
		this._log($"menu {node.Id}: {node.Domain}, {node.Buttons.Count} focus state(s)");
		return menu;
	}

	private ButtonAction Activate(MenuInfo menu, FocusState state)
	{
		var keys = new List<NavKey>(menu.ReachPath);
		keys.AddRange(state.Keys);
		using var session = this.Reach(keys, out _);
		if (session is null)
		{
			return Unknown("The menu could not be reached again.");
		}

		var playlistBefore = session.Playlist;
		var audioBefore = session.AudioStream;
		var subtitleBefore = session.SubtitleStream;
		session.Press(BlurayNative.KeyEnter);
		session.Pump(
			() => session.Playlist != playlistBefore && !session.MenuActive,
			48,
			this._bdj ? TimeSpan.FromSeconds(1.5) : TimeSpan.FromMilliseconds(200),
			8_000,
			this.Remaining(TimeSpan.FromSeconds(20)));

		if (session.Playlist is { } playlist && playlist != playlistBefore && !session.MenuActive)
		{
			return this.FollowPlaylist(session, playlist);
		}

		var fingerprint = session.Overlay.Fingerprint();
		if (session.Overlay.IsVisible && !menu.States.Any(s => s.Fingerprint == fingerprint))
		{
			var target = this._menusByFingerprint.GetValueOrDefault(fingerprint)
				?? this.DiscoverMenu([.. keys, NavKey.Enter], menu);
			return target is null
				? Unknown("Opens a menu that could not be recorded.")
				: new ButtonAction { Type = ButtonActionType.OpenMenu, MenuId = target.Node.Id, Confidence = ActionConfidence.Observed };
		}

		if (session.AudioStream != audioBefore && session.AudioStream is { } audio)
		{
			return new ButtonAction { Type = ButtonActionType.ChangeSetting, Setting = "Audio", Value = audio, Confidence = ActionConfidence.Observed };
		}

		if (session.SubtitleStream != subtitleBefore && session.SubtitleStream is { } subtitle)
		{
			return new ButtonAction { Type = ButtonActionType.ChangeSetting, Setting = "Subtitle", Value = subtitle, Confidence = ActionConfidence.Observed };
		}

		return Unknown("No playback or menu change was observed.");
	}

	private ButtonAction FollowPlaylist(BluraySession session, int playlist)
	{
		// Let the first chapter event arrive.
		session.Pump(() => session.Chapter is not null, 0, TimeSpan.Zero, 400, this.Remaining(TimeSpan.FromSeconds(5)));
		var start = session.Chapter ?? 1;
		var info = this._map.Titles.FirstOrDefault(t => t.Playlist == playlist);
		var chapterCount = info?.ChapterCount ?? session.ReadPlaylist(playlist)?.ChapterCount ?? 0;

		session.SeekNearEnd();
		var endsBefore = session.EndOfTitleCount;
		session.Pump(
			() => this.IsMenuShowing(session) || session.Playlist != playlist || session.EndOfTitleCount > endsBefore,
			0,
			TimeSpan.Zero,
			20_000,
			this.Remaining(TimeSpan.FromSeconds(30)));

		string then;
		string? thenMenu = null;
		int? thenTitle = null;
		if (this.IsMenuShowing(session))
		{
			session.Pump(null, 24, TimeSpan.Zero, 2_000, this.Remaining(TimeSpan.FromSeconds(5)));
			then = "ReturnToMenu";
			thenMenu = this._menusByFingerprint.GetValueOrDefault(session.Overlay.Fingerprint())?.Node.Id;
		}
		else if (session.Playlist is { } next && next != playlist)
		{
			then = "PlayTitle";
			thenTitle = next;
		}
		else
		{
			then = session.EndOfTitleCount > endsBefore ? "Stop" : "Unknown";
		}

		return new ButtonAction
		{
			Type = ButtonActionType.PlayTitle,
			Playlist = playlist,
			StartChapter = start,
			EndChapter = chapterCount > 0 ? chapterCount : null,
			TitleChapterCount = chapterCount > 0 ? chapterCount : null,
			CoversWholeTitle = start == 1,
			Then = then,
			ThenMenuId = thenMenu,
			ThenTitle = thenTitle,
			Confidence = this._bdj ? ActionConfidence.Inferred : ActionConfidence.Observed,
		};
	}

	/// <summary>
	/// The highlighted button is where a focus state differs from the most common value of each pixel across all
	/// focus states (every other button is drawn in its normal state in most of them).
	/// </summary>
	private static List<ButtonRect> InferRects(List<FocusState> states, int width, int height)
	{
		var rects = states.Select(_ => new ButtonRect()).ToList();
		if (states.Count < 2 || width == 0 || height == 0)
		{
			return rects;
		}

		var pixels = width * height;
		var mode = new uint[pixels];
		var counts = new Dictionary<uint, int>();
		for (var p = 0; p < pixels; p++)
		{
			counts.Clear();
			foreach (var state in states)
			{
				if (p < state.Plane.Length)
				{
					counts[state.Plane[p]] = counts.GetValueOrDefault(state.Plane[p]) + 1;
				}
			}

			mode[p] = counts.Count == 0 ? 0 : counts.MaxBy(kv => kv.Value).Key;
		}

		for (var i = 0; i < states.Count; i++)
		{
			int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
			var plane = states[i].Plane;
			for (var p = 0; p < Math.Min(pixels, plane.Length); p++)
			{
				if (plane[p] == mode[p])
				{
					continue;
				}

				var x = p % width;
				var y = p / width;
				minX = Math.Min(minX, x);
				minY = Math.Min(minY, y);
				maxX = Math.Max(maxX, x);
				maxY = Math.Max(maxY, y);
			}

			if (maxX >= 0)
			{
				rects[i] = new ButtonRect
				{
					X = minX * OverlayCompositor.Step,
					Y = minY * OverlayCompositor.Step,
					W = (maxX - minX + 1) * OverlayCompositor.Step,
					H = (maxY - minY + 1) * OverlayCompositor.Step,
				};
			}
		}

		return rects;
	}

	private static List<NavKey>? Route(Dictionary<(int From, NavKey Key), int> edges, int from, int to)
	{
		if (from == to)
		{
			return [];
		}

		var previous = new Dictionary<int, (int From, NavKey Key)>();
		var queue = new Queue<int>([from]);
		while (queue.Count > 0)
		{
			var node = queue.Dequeue();
			foreach (var ((source, key), target) in edges)
			{
				if (source != node || target == from || previous.ContainsKey(target))
				{
					continue;
				}

				previous[target] = (node, key);
				if (target == to)
				{
					var keys = new List<NavKey>();
					for (var n = to; n != from; n = previous[n].From)
					{
						keys.Add(previous[n].Key);
					}

					keys.Reverse();
					return keys;
				}

				queue.Enqueue(target);
			}
		}

		return null;
	}

	private static int? Neighbour(Dictionary<(int From, NavKey Key), int> edges, int state, NavKey key) =>
		edges.TryGetValue((state, key), out var target) && target != state ? target + 1 : null;

	private TimeSpan Remaining(TimeSpan cap)
	{
		var left = this._options.TimeBudget - this._clock.Elapsed;
		return left < cap ? (left < TimeSpan.Zero ? TimeSpan.Zero : left) : cap;
	}

	private bool HasBudget()
	{
		if (this._actions >= this._options.MaxActions)
		{
			this.MarkIncomplete($"Button limit ({this._options.MaxActions}) reached; some buttons were not followed.");
			return false;
		}

		if (this._clock.Elapsed > this._options.TimeBudget)
		{
			this.MarkIncomplete($"Time budget ({this._options.TimeBudget.TotalSeconds:0}s) reached; some buttons were not followed.");
			return false;
		}

		return true;
	}

	private void MarkIncomplete(string warning)
	{
		this._map.Complete = false;
		if (!this._map.Warnings.Contains(warning))
		{
			this._map.Warnings.Add(warning);
		}
	}

	private static ButtonAction Unknown(string reason) => new() { Type = ButtonActionType.Unknown, Reason = reason };

	private sealed record FocusState(ulong Fingerprint, List<NavKey> Keys, uint[] Plane);

	private sealed record MenuInfo(MenuNode Node, List<NavKey> ReachPath, List<FocusState> States);
}
