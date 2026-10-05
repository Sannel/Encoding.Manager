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
	private const int MaxButtonsPerMenu = 96;

	/// <summary>
	/// Fresh sessions one menu's focus walk may open after leaving the menu. Each BD-J restart takes ~20s and leaves
	/// threads behind in the shared JVM (which eventually wedges libbluray), so the number is kept small.
	/// </summary>
	private const int MaxReopensPerMenu = 12;
	private static readonly NavKey[] _directions = [NavKey.Down, NavKey.Right, NavKey.Up, NavKey.Left];

	private readonly CrawlOptions _options;
	private readonly Action<string> _log;
	private readonly List<MenuInfo> _menus = [];

	/// <summary>Submenus found by activating a button, mapped once the current menu's buttons are done.</summary>
	private readonly Queue<PendingMenu> _pendingMenus = new();
	private readonly Dictionary<ulong, MenuInfo> _menusByFingerprint = [];
	private DiscMenuMap _map = new();
	private Stopwatch _clock = new();
	private string _path = string.Empty;
	private bool _bdj;
	private string? _reachFailure;

	/// <summary>Null until known; true when menus are only reachable by pressing Title Menu straight away.</summary>
	private bool? _directTopMenu;
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

			// Buttons closest to the menu's start first (menu bar, lists, first chapter pages), so that when the
			// time budget runs out it is the deep, repetitive ones that are left without an action.
			foreach (var b in Enumerable.Range(0, menu.States.Count).OrderBy(s => menu.States[s].Keys.Count))
			{
				if (!this.HasBudget())
				{
					return this.Finish();
				}

				this._actions++;
				menu.Node.Buttons[b].Action = this.Activate(menu, menu.States[b]);
				this.ReportProgress();
			}

			// Now map the submenus this menu's buttons opened; their own buttons follow in later iterations.
			while (this._pendingMenus.Count > 0)
			{
				if (!this.HasBudget())
				{
					return this.Finish();
				}

				var pending = this._pendingMenus.Dequeue();
				var action = pending.Parent.Node.Buttons[pending.Button].Action;
				if (this.DiscoverMenu(pending.Path, pending.Parent, pending.OpenedFrom) is { } target)
				{
					action.MenuId = target.Node.Id;
					action.Reason = null;
				}
				else
				{
					pending.Parent.Node.Buttons[pending.Button].Action = Unknown("Opens a menu that could not be recorded.");
				}

				this.ReportProgress();
			}
		}

		return this.Finish();
	}

	/// <summary>
	/// Called (on the crawl thread) with the map so far after each menu is recorded and each button is followed, so a
	/// host can save progress — a BD-J crawl can wedge in native code and have to be killed.
	/// </summary>
	public Action<DiscMenuMap>? Progress { get; set; }

	private void ReportProgress() => this.Progress?.Invoke(this.Finish());

	private DiscMenuMap Finish()
	{
		foreach (var title in this._map.Titles)
		{
			title.ReachedFromButtons.Clear();
		}

		// Buttons the crawl did not get to are marked as such, not left looking like "pressed, nothing happened".
		foreach (var button in this._map.Menus.SelectMany(m => m.Buttons).Where(b => b.Action.Type == ButtonActionType.Unknown && b.Action.Reason is null))
		{
			button.Action.Reason = "Not followed: the crawl's time budget ran out first. Use the button's screenshot to see what it is.";
		}

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

	/// <summary>
	/// Opens a session and gets to the first menu, then replays <paramref name="keys"/>. The first attempt presses
	/// Title Menu straight away, like VLC's "Title Menu" — the screenshot renderer does the same, so both skip the
	/// same logos and trailers and land on the same menu. If that shows no menu, a fresh session lets first play run
	/// (pressing Title Menu if it never shows a menu). Whichever works is reused.
	/// </summary>
	private BluraySession? Reach(List<NavKey> keys, out bool usedTopMenu)
	{
		usedTopMenu = false;
		BluraySession? session = null;
		string? titleMenuFailure = null;
		if (this._directTopMenu != false)
		{
			session = this.TryReach(directTopMenu: true, out usedTopMenu, out titleMenuFailure);
			if (session is not null)
			{
				this._directTopMenu ??= true;
			}
			else if (this._directTopMenu == true)
			{
				this._reachFailure = titleMenuFailure;
				return null;
			}
		}

		if (session is null)
		{
			session = this.TryReach(directTopMenu: false, out usedTopMenu, out var firstPlayFailure);
			if (session is null)
			{
				this._reachFailure = titleMenuFailure is null
					? firstPlayFailure
					: $"Title menu: {titleMenuFailure} First play: {firstPlayFailure}";
				return null;
			}

			this._directTopMenu = false;
		}

		this.SettleIntro(session);
		this._log($"reach settle: {this.Settle(session)}, playlist {session.Playlist}, overlay {session.Overlay.IsVisible}, flushes {session.Overlay.FlushCount}");

		// Paths recorded from a Title Menu start begin with that key; it was just pressed.
		var replay = usedTopMenu && keys.Count > 0 && keys[0] == NavKey.TitleMenu ? keys.Skip(1) : keys;
		foreach (var key in replay)
		{
			this.Press(session, key);
		}

		return session;
	}

	private BluraySession? TryReach(bool directTopMenu, out bool usedTopMenu, out string? failure)
	{
		usedTopMenu = directTopMenu;
		failure = null;
		var session = BluraySession.Open(this._path, this._options.MenuLanguage);
		if (!session.Play())
		{
			failure = session.FailureReason;
			session.Dispose();
			return null;
		}

		// BD-J (Java) needs time to start its JVM and Xlet before anything is drawn.
		var wait = this.Remaining(TimeSpan.FromSeconds(this._bdj ? 60 : 30));
		if (directTopMenu)
		{
			session.TopMenu();
		}

		session.Pump(() => this.IsMenuShowing(session), 0, TimeSpan.Zero, 40_000, wait);
		if (!directTopMenu && !this.IsMenuShowing(session) && !session.Failed)
		{
			// Trailers / an auto-playing feature: jump to the top menu.
			session.TopMenu();
			usedTopMenu = true;
			session.Pump(() => this.IsMenuShowing(session), 0, TimeSpan.Zero, 40_000, this.Remaining(TimeSpan.FromSeconds(30)));
		}

		if (this.IsMenuShowing(session))
		{
			return session;
		}

		failure = session.FailureReason
			?? $"No menu graphics appeared (playlist {session.Playlist?.ToString() ?? "none"}, menu event {(session.MenuActive ? "on" : "off")}, {session.IdleEvents} idle event(s), overlay {(session.Overlay.FlushCount > 0 ? $"drew {session.Overlay.FlushCount} frame(s)" : "never drew")}).";
		session.Dispose();
		return null;
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

	/// <summary>
	/// Reads until the graphics stop changing. BD-J menus react on a Java thread and animate their highlight, so they
	/// get a longer quiet period than HDMV; HDMV still needs a short one, as menu video reads arrive faster than the
	/// highlight is drawn.
	/// </summary>
	private string Settle(BluraySession session) =>
		session.Pump(null, 4, this._bdj ? TimeSpan.FromMilliseconds(1500) : TimeSpan.FromMilliseconds(400), 8_000, this.Remaining(TimeSpan.FromSeconds(this._bdj ? 15 : 10)));

	/// <summary>
	/// After a menu first appears, BD-J discs usually play an intro animation (graphics sliding in, highlight fading
	/// up). Exploring before it finishes records animation frames as "buttons", so wait for ~2s with no new graphics.
	/// </summary>
	private void SettleIntro(BluraySession session)
	{
		if (this._bdj)
		{
			session.Pump(null, 4, TimeSpan.FromSeconds(2), 40_000, this.Remaining(TimeSpan.FromSeconds(30)));
		}
	}

	/// <summary>Reaches a menu, walks its focus states with the arrow keys and records it as a node.</summary>
	private MenuInfo? DiscoverMenu(List<NavKey> path, MenuInfo? parent, uint[]? openedFrom = null)
	{
		using var session = this.Reach(path, out var usedTopMenu);
		return session is null
			? null
			: this.DiscoverIn(session, usedTopMenu && path.Count == 0 ? [NavKey.TitleMenu] : path, parent, openedFrom);
	}

	/// <summary>
	/// Records the menu <paramref name="session"/> is showing (reached with <paramref name="reachPath"/>). When
	/// <paramref name="openedFrom"/> (the graphics plane before Enter) is given, the menu may be a panel that opened
	/// on the same screen, and the focus walk stays inside the area that changed when it opened (a full-screen
	/// page change makes that area the whole screen, so nothing is excluded).
	/// </summary>
	private MenuInfo? DiscoverIn(BluraySession session, List<NavKey> reachPath, MenuInfo? parent, uint[]? openedFrom = null)
	{
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
		var menuId = $"m{this._menus.Count}";
		this.SaveOverlay(session, menuId, 1);
		var sampledWidth = session.Overlay.SampledSize.Width;
		var panel = openedFrom is null ? null : DiffBox(openedFrom, states[0].Plane, sampledWidth)?.Inflate(2);
		var edges = new Dictionary<(int From, NavKey Key), int>();
		var current = 0;
		var pending = new Queue<(int State, NavKey Key)>(_directions.Select(d => (0, d)));

		// The walk may leave this menu (a key that closes a panel, an auto-action button); it then continues in a
		// fresh session reached the same way. The old session is closed first — two BD-J sessions in one JVM
		// deadlock. (Disposing is idempotent, so the caller's own dispose of the first session is harmless.)
		var active = session;
		var reopens = 0;
		var deferred = 0;
		bool Reopen()
		{
			active.Dispose();
			active = session;
			if (this.Reach(reachPath, out _) is not { } fresh)
			{
				return false;
			}

			active = fresh;
			current = 0;
			return fresh.Overlay.Fingerprint() == states[0].Fingerprint;
		}

		// Moves to a known state and checks it got there: some menus remember their last focus (a BD-J list reopens
		// on the item focused before), so a recorded route can land elsewhere. Re-routes from where it landed.
		// Every key press is checked: landing on an unrecorded state is a discovery (recorded, its directions queued),
		// landing on a known one updates the edge to what the menu really did.
		bool RouteTo(int target)
		{
			for (var attempt = 0; attempt < 4; attempt++)
			{
				if (current == target)
				{
					return true;
				}

				if (Route(edges, current, target) is not { } route)
				{
					return false;
				}

				foreach (var step in route)
				{
					var expected = edges.GetValueOrDefault((current, step), -1);
					this.Press(active, step);
					var landed = active.Overlay.Fingerprint();
					var at = states.FindIndex(s => s.Fingerprint == landed);
					if (at < 0)
					{
						var plane = active.Overlay.Snapshot();
						if (!active.Overlay.IsVisible || (panel is { } area && LeftPanel(states[0].Plane, plane, area, sampledWidth)) || states.Count >= MaxButtonsPerMenu)
						{
							return false;
						}

						states.Add(new FocusState(landed, [.. states[current].Keys, step], plane));
						at = states.Count - 1;
						this.SaveOverlay(active, menuId, states.Count);
						foreach (var d in _directions)
						{
							pending.Enqueue((at, d));
						}
					}

					edges[(current, step)] = at;
					current = at;
					if (at != expected)
					{
						// Off the planned route: plan again from here.
						break;
					}
				}
			}

			return current == target;
		}

		// One menu's walk may use at most half of the remaining budget, leaving time to activate its buttons; a
		// submenu (often settings panels) gets at most two minutes.
		var walkTime = (this._options.TimeBudget - this._clock.Elapsed) / 2;
		var walkEnds = this._clock.Elapsed + (parent is null ? walkTime : TimeSpan.FromTicks(Math.Min(walkTime.Ticks, TimeSpan.FromMinutes(2).Ticks)));
		while (pending.Count > 0 && states.Count < MaxButtonsPerMenu && this._clock.Elapsed < walkEnds)
		{
			var (from, key) = pending.Dequeue();
			if (Route(edges, current, from) is null && deferred < pending.Count)
			{
				// Not reachable from here with the edges known so far: try the other pending moves first, and start
				// over from the menu's first state only when none of them can be reached either.
				pending.Enqueue((from, key));
				deferred++;
				continue;
			}

			deferred = 0;
			if (!RouteTo(from) && (++reopens > MaxReopensPerMenu || !Reopen() || !RouteTo(from)))
			{
				continue;
			}

			this.Press(active, key);
			var now = active.Overlay.Fingerprint();
			var index = states.FindIndex(s => s.Fingerprint == now);
			var plane = active.Overlay.Snapshot();
			var left = !active.MenuActive && !active.Overlay.IsVisible;
			if (!left && index < 0 && panel is { } area)
			{
				left = LeftPanel(states[0].Plane, plane, area, sampledWidth);
			}

			if (left)
			{
				// The key left this menu (e.g. a panel closed and focus went back to the menu bar): what it shows
				// belongs to another menu. Do not record it; step back, or start again from this menu.
				this._log($"walk: {key} from state {from + 1} left the menu");
				// Even when the graphics went away: a BD-J menu bar hidden by one key usually comes back with the opposite.
				this.Press(active, Opposite(key));

				if (active.Overlay.Fingerprint() == states[from].Fingerprint)
				{
					current = from;
				}
				else if (++reopens > MaxReopensPerMenu || !Reopen())
				{
					break;
				}

				continue;
			}

			if (index < 0)
			{
				states.Add(new FocusState(now, [.. states[from].Keys, key], plane));
				index = states.Count - 1;
				this.SaveOverlay(active, menuId, states.Count);
				foreach (var d in _directions)
				{
					pending.Enqueue((index, d));
				}
			}

			edges[(from, key)] = index;
			current = index;
		}

		if (active != session)
		{
			active.Dispose();
		}

		if (pending.Count > 0)
		{
			this.MarkIncomplete($"Menu {menuId}: not every button was explored (time or button limit); its screenshots may show more buttons than were mapped.");
		}

		var sampledHeight = session.Overlay.SampledSize.Height;
		DumpStates(menuId, states, sampledWidth, sampledHeight);
		var rects = InferRects(states, sampledWidth, sampledHeight);
		var node = new MenuNode
		{
			Id = menuId,
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
				FocusPath = [.. s.Keys],
				Neighbours = new ButtonNeighbours
				{
					Up = Neighbour(edges, i, NavKey.Up),
					Down = Neighbour(edges, i, NavKey.Down),
					Left = Neighbour(edges, i, NavKey.Left),
					Right = Neighbour(edges, i, NavKey.Right),
				},
			}).ToList(),
		};

		var menu = new MenuInfo(node, reachPath, states, edges);
		this._menus.Add(menu);
		this._menusByFingerprint[fingerprint] = menu;
		this._map.Menus.Add(node);
		this._log($"menu {node.Id}: {node.Domain}, {node.Buttons.Count} focus state(s)");
		this.ReportProgress();
		return menu;
	}

	private ButtonAction Activate(MenuInfo menu, FocusState state)
	{
		using var session = this.Reach(menu.ReachPath, out _);
		if (session is null)
		{
			return Unknown("The menu could not be reached again.");
		}

		// Move the focus by fingerprint rather than replaying the walk's key path: menus that remember their last
		// focus make that path land elsewhere in a fresh session. The keys that verifiably worked become the
		// button's focus path.
		var pressed = new List<NavKey>();
		var stateIndex = menu.States.IndexOf(state);
		if (!this.NavigateTo(session, menu, stateIndex, pressed))
		{
			return Unknown("The button could not be focused again from the start of its menu.");
		}

		menu.Node.Buttons[stateIndex].FocusPath = pressed;
		var keys = new List<NavKey>(menu.ReachPath);
		keys.AddRange(pressed);

		var playlistBefore = session.Playlist;
		var audioBefore = session.AudioStream;
		var subtitleBefore = session.SubtitleStream;
		var flushBefore = session.Overlay.FlushCount;
		var fingerprintBefore = session.Overlay.Fingerprint();
		var planeBefore = session.Overlay.Snapshot();
		var clock = Stopwatch.StartNew();
		session.Press(BlurayNative.KeyEnter);

		// Done when playback starts, or when the graphics changed and then stayed still (a submenu or a setting).
		// BD-J menus never clear BD_EVENT_MENU, so for them any playlist change counts as "playback started".
		var quiet = this._bdj ? TimeSpan.FromSeconds(2) : TimeSpan.FromMilliseconds(500);
		var lastFlush = flushBefore;
		var lastChange = clock.Elapsed;
		bool Started() => session.Playlist != playlistBefore && (this._bdj || !session.MenuActive);
		var pumped = session.Pump(
			() =>
			{
				if (Started())
				{
					return true;
				}

				var flush = session.Overlay.FlushCount;
				if (flush != lastFlush)
				{
					lastFlush = flush;
					lastChange = clock.Elapsed;
					return false;
				}

				// A hidden overlay means the menu is going away: keep waiting for the playlist.
				return flush != flushBefore && session.Overlay.IsVisible && clock.Elapsed - lastChange >= quiet;
			},
			0,
			TimeSpan.Zero,
			40_000,
			this.Remaining(TimeSpan.FromSeconds(this._bdj ? 15 : 10)));
		this._log($"{menu.Node.Id} [{string.Join(",", state.Keys)}] Enter: {pumped}, playlist {playlistBefore} -> {session.Playlist}");

		if (this._bdj && Started() && !this.IsFeaturePlaylist(session.Playlist))
		{
			// BD-J discs often play a short transition / black playlist before the real one; wait for a playlist
			// that is long enough to be a title, or for the menu to come back.
			var transition = session.Playlist;
			var waited = session.Pump(
				() => this.IsFeaturePlaylist(session.Playlist) || (session.Playlist != transition && session.Overlay.IsVisible),
				0,
				TimeSpan.Zero,
				40_000,
				this.Remaining(TimeSpan.FromSeconds(15)));
			this._log($"{menu.Node.Id} transition from playlist {transition}: {waited}, now {session.Playlist}");
		}

		if (session.Playlist is { } playlist && Started() && (!this._bdj || this.IsFeaturePlaylist(playlist)))
		{
			return this.FollowPlaylist(session, playlist);
		}

		var fingerprint = session.Overlay.Fingerprint();
		if (session.Overlay.IsVisible && !menu.States.Any(s => s.Fingerprint == fingerprint))
		{
			if (this._menusByFingerprint.GetValueOrDefault(fingerprint) is { } known)
			{
				return new ButtonAction { Type = ButtonActionType.OpenMenu, MenuId = known.Node.Id, Confidence = ActionConfidence.Observed };
			}

			// Mapped after this menu's own buttons (see Crawl), so a submenu's walk cannot use up the time this
			// menu's buttons need.
			this._pendingMenus.Enqueue(new PendingMenu(menu, stateIndex, [.. keys, NavKey.Enter], planeBefore));
			return new ButtonAction { Type = ButtonActionType.OpenMenu, Confidence = ActionConfidence.Observed, Reason = "Opens a menu that was not mapped (the crawl's time budget ran out first)." };
		}

		if (session.AudioStream != audioBefore && session.AudioStream is { } audio)
		{
			return new ButtonAction { Type = ButtonActionType.ChangeSetting, Setting = "Audio", Value = audio, Confidence = ActionConfidence.Observed };
		}

		if (session.SubtitleStream != subtitleBefore && session.SubtitleStream is { } subtitle)
		{
			return new ButtonAction { Type = ButtonActionType.ChangeSetting, Setting = "Subtitle", Value = subtitle, Confidence = ActionConfidence.Observed };
		}

		var redraws = session.Overlay.FlushCount - flushBefore;
		return Unknown(
			$"No playback or menu change was observed in {clock.Elapsed.TotalSeconds:0}s after Enter " +
			$"(playlist {playlistBefore?.ToString() ?? "none"} -> {session.Playlist?.ToString() ?? "none"}, " +
			$"graphics redrawn {redraws} time(s){(redraws > 0 && session.Overlay.Fingerprint() == fingerprintBefore ? " but ended unchanged" : string.Empty)}, " +
			$"overlay {(session.Overlay.IsVisible ? "visible" : "hidden")}{(session.Failed ? $", playback failed: {session.FailureReason}" : string.Empty)}).");
	}

	/// <summary>
	/// Moves the focus of a session showing <paramref name="menu"/> to focus state <paramref name="target"/> using
	/// the walk's edges, checking each landing by fingerprint and re-routing from where it actually landed.
	/// </summary>
	private bool NavigateTo(BluraySession session, MenuInfo menu, int target, List<NavKey> pressed)
	{
		int Where()
		{
			var fingerprint = session.Overlay.Fingerprint();
			return menu.States.FindIndex(s => s.Fingerprint == fingerprint);
		}

		var current = Where();
		for (var attempt = 0; attempt < 4 && current >= 0; attempt++)
		{
			if (current == target)
			{
				return true;
			}

			if (Route(menu.Edges, current, target) is not { } route)
			{
				return false;
			}

			foreach (var step in route)
			{
				this.Press(session, step);
				pressed.Add(step);
			}

			current = Where();
		}

		return current == target;
	}

	/// <summary>True for a playlist libbluray lists as a title (at least a minute long, duplicates removed).</summary>
	private bool IsFeaturePlaylist(int? playlist) =>
		playlist is { } p && this._map.Titles.Any(t => t.Playlist == p);

	private ButtonAction FollowPlaylist(BluraySession session, int playlist)
	{
		// Let the first chapter event arrive, then keep reading briefly: a button that plays from a chapter mark
		// starts the playlist (chapter 1 is reported) and then seeks to the mark, reporting the real chapter.
		session.Pump(() => session.Chapter is not null, 0, TimeSpan.Zero, 400, this.Remaining(TimeSpan.FromSeconds(5)));
		session.Pump(null, 8, TimeSpan.FromMilliseconds(500), 2_000, this.Remaining(TimeSpan.FromSeconds(3)));
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
			this.Remaining(TimeSpan.FromSeconds(this._bdj ? 10 : 30)));

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

	/// <summary>Saves the BD-J graphics plane of a focus state for the screenshot renderer (see CrawlOptions.OverlayFolder).</summary>
	private void SaveOverlay(BluraySession session, string menuId, int button)
	{
		if (!this._bdj || this._options.OverlayFolder is not { } folder)
		{
			return;
		}

		try
		{
			Directory.CreateDirectory(folder);
			session.Overlay.SaveArgbPlane(Path.Combine(folder, $"{menuId}-s{button}-overlay.png"));
		}
		catch (IOException ex)
		{
			this._log($"overlay {menuId}-s{button} not saved: {ex.Message}");
		}
	}

	/// <summary>
	/// Diagnostics: when DISC_MENU_DUMP_DIR is set, writes each focus state's graphics plane as a PPM image (on
	/// white, so transparent areas are visible) for checking what the crawler saw.
	/// </summary>
	private static void DumpStates(string menuId, List<FocusState> states, int width, int height)
	{
		if (Environment.GetEnvironmentVariable("DISC_MENU_DUMP_DIR") is not { Length: > 0 } folder || width <= 0 || height <= 0)
		{
			return;
		}

		Directory.CreateDirectory(folder);
		for (var i = 0; i < states.Count; i++)
		{
			// One filter byte (0 = none) per row, then RGB.
			var raw = new byte[height * ((width * 3) + 1)];
			var plane = states[i].Plane;
			for (var p = 0; p < Math.Min(plane.Length, width * height); p++)
			{
				var argb = plane[p];
				var a = (argb >> 24) & 0xFF;
				var offset = ((p / width) * ((width * 3) + 1)) + 1 + ((p % width) * 3);
				for (var c = 0; c < 3; c++)
				{
					var value = (argb >> (16 - (c * 8))) & 0xFF;
					raw[offset + c] = (byte)(((value * a) + (255 * (255 - a))) / 255);
				}
			}

			File.WriteAllBytes(Path.Combine(folder, $"{menuId}-s{i + 1}.png"), PngWriter.Encode(width, height, 2, raw));
		}
	}

	/// <summary>Bounding box (in sampled pixels) of where two planes differ, or null when they are identical.</summary>
	private static Box? DiffBox(uint[] a, uint[] b, int width)
	{
		if (width <= 0)
		{
			return null;
		}

		int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
		for (var p = 0; p < Math.Min(a.Length, b.Length); p++)
		{
			if (a[p] == b[p])
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

		return maxX < 0 ? null : new Box(minX, minY, maxX, maxY);
	}

	/// <summary>
	/// True when <paramref name="now"/> no longer shows the panel that <paramref name="start"/> showed: the change
	/// reaches outside the panel's area, or most of the panel's visible pixels disappeared (it closed). Moving the
	/// highlight only recolours a small part of the panel.
	/// </summary>
	private static bool LeftPanel(uint[] start, uint[] now, Box area, int width)
	{
		if (DiffBox(start, now, width) is not { } moved)
		{
			return false;
		}

		if (!area.Contains(moved))
		{
			return true;
		}

		var shown = 0;
		var gone = 0;
		for (var y = Math.Max(0, area.MinY); y <= area.MaxY; y++)
		{
			for (var x = Math.Max(0, area.MinX); x <= area.MaxX && x < width; x++)
			{
				var p = (y * width) + x;
				if (p >= start.Length || p >= now.Length || start[p] == 0)
				{
					continue;
				}

				shown++;
				if (now[p] == 0)
				{
					gone++;
				}
			}
		}

		return shown > 0 && gone * 2 > shown;
	}

	private static NavKey Opposite(NavKey key) => key switch
	{
		NavKey.Up => NavKey.Down,
		NavKey.Down => NavKey.Up,
		NavKey.Left => NavKey.Right,
		_ => NavKey.Left,
	};

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

	private sealed record PendingMenu(MenuInfo Parent, int Button, List<NavKey> Path, uint[] OpenedFrom);

	private sealed record Box(int MinX, int MinY, int MaxX, int MaxY)
	{
		public Box Inflate(int by) => new(this.MinX - by, this.MinY - by, this.MaxX + by, this.MaxY + by);

		public bool Contains(Box other) =>
			other.MinX >= this.MinX && other.MinY >= this.MinY && other.MaxX <= this.MaxX && other.MaxY <= this.MaxY;
	}

	private sealed record FocusState(ulong Fingerprint, List<NavKey> Keys, uint[] Plane);

	private sealed record MenuInfo(MenuNode Node, List<NavKey> ReachPath, List<FocusState> States, Dictionary<(int From, NavKey Key), int> Edges);
}
