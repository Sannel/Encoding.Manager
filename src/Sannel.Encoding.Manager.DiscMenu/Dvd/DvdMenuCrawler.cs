using System.Diagnostics;
using Sannel.Encoding.Manager.DiscMenu.Model;
using Sannel.Encoding.Manager.DiscMenu.Native;

namespace Sannel.Encoding.Manager.DiscMenu.Dvd;

/// <summary>
/// Maps a DVD's menus by running its own navigation VM (libdvdnav): every button of every reachable menu is
/// activated on a VM snapshot and followed until it reaches another menu or plays a title, whose chapter range
/// is followed (cell commands included) until playback leaves the title.
/// </summary>
public sealed class DvdMenuCrawler
{
	private static readonly NavKey[] _directions = [NavKey.Up, NavKey.Down, NavKey.Left, NavKey.Right];

	private readonly CrawlOptions _options;
	private readonly Action<string> _log;
	private readonly Dictionary<string, MenuState> _menusByKey = new(StringComparer.Ordinal);
	private readonly List<MenuState> _menus = [];
	private readonly List<DvdVm> _owners = [];
	private readonly Queue<MenuState> _queue = new();
	private DiscMenuMap _map = new();
	private Stopwatch _clock = new();
	private int _actions;
	private (int Width, int Height) _frame = (720, 480);

	public DvdMenuCrawler(CrawlOptions options, Action<string>? log = null)
	{
		this._options = options;
		this._log = log ?? (_ => { });
	}

	/// <summary>Crawls the DVD at <paramref name="path"/> (folder containing VIDEO_TS, or VIDEO_TS itself).</summary>
	public DiscMenuMap Crawl(string path)
	{
		this._clock = Stopwatch.StartNew();
		this._map = new DiscMenuMap { DiscType = "DVD", MenuSystem = "DVD" };

		try
		{
			this.ReadTitles(this.OpenOwner(path));
			this.CrawlFrom(path);
		}
		finally
		{
			// Snapshots share IFO data with the handle they were copied from, so they go first.
			foreach (var menu in this._menus)
			{
				menu.Snapshot.Dispose();
			}

			foreach (var owner in this._owners)
			{
				owner.Dispose();
			}
		}

		this.BuildReachPaths();
		this.FillReverseIndex();
		this._map.CrawlMilliseconds = (int)this._clock.ElapsedMilliseconds;
		return this._map;
	}

	private void ReadTitles(DvdVm root)
	{
		for (var t = 1; t <= root.TitleCount; t++)
		{
			this._map.Titles.Add(new DiscTitle
			{
				DvdTitle = t,
				ChapterCount = root.PartCount(t),
				DurationSeconds = root.TitleDurationSeconds(t),
			});
		}
	}

	/// <summary>Opens a handle that lives until the crawl ends (snapshots copied from it borrow its IFO data).</summary>
	private DvdVm OpenOwner(string path)
	{
		var vm = DvdVm.Open(path, this._options.MenuLanguage);
		this._owners.Add(vm);
		return vm;
	}

	private void CrawlFrom(string path)
	{
		var start = this.OpenOwner(path);
		var arrival = this.RunToArrival(start);

		// First play: intros / warnings / an auto-playing feature before any menu.
		for (var guard = 0; arrival.Kind == ArrivalKind.Title && guard < 8; guard++)
		{
			var follow = this.FollowTitle(start, arrival.Title, arrival.Part);
			this._map.FirstPlay ??= this.ToPlayAction(follow);
			arrival = follow.Next;
		}

		var firstPath = new List<PathStep>();
		MenuState? first = null;
		if (arrival.Kind == ArrivalKind.Menu)
		{
			first = this.GetOrAddMenu(start, arrival, null, firstPath);
		}
		else
		{
			// No menu on the first-play path: ask for the title or root menu directly.
			foreach (var (menuId, key) in new[] { (DvdNavNative.MenuTitle, NavKey.TitleMenu), (DvdNavNative.MenuRoot, NavKey.RootMenu) })
			{
				var fresh = this.OpenOwner(path);
				fresh.Step();
				if (fresh.MenuCall(menuId) && this.RunToArrival(fresh) is { Kind: ArrivalKind.Menu } called)
				{
					first = this.GetOrAddMenu(fresh, called, null, [new PathStep(null, 0, key)]);
					break;
				}
			}
		}

		if (first is null)
		{
			this._map.MenuSystem = "None";
			this._map.Warnings.Add("No menus were found on this disc.");
			return;
		}

		// Menus that are only reachable with the remote's Title / Root keys.
		foreach (var (menuId, key) in new[] { (DvdNavNative.MenuTitle, NavKey.TitleMenu), (DvdNavNative.MenuRoot, NavKey.RootMenu) })
		{
			using var probe = first.Snapshot.Duplicate();
			if (probe.MenuCall(menuId) && this.RunToArrival(probe) is { Kind: ArrivalKind.Menu } called)
			{
				this.GetOrAddMenu(probe, called, first, [.. first.Path, new PathStep(null, 0, key)]);
			}
		}

		while (this._queue.Count > 0)
		{
			var menu = this._queue.Dequeue();
			foreach (var button in menu.Node.Buttons)
			{
				if (!this.HasBudget())
				{
					return;
				}

				this._actions++;
				button.Action = this.Activate(menu, button.Number);
			}
		}
	}

	private ButtonAction Activate(MenuState menu, int button)
	{
		using var vm = menu.Snapshot.Duplicate();
		var audioBefore = vm.AudioStream;
		var spuBefore = vm.SpuStream;
		if (!vm.SelectAndActivate(button))
		{
			return Unknown("The button could not be activated.");
		}

		var path = new List<PathStep>(menu.Path) { new(menu.Node.Id, button, null) };
		var arrival = this.RunToArrival(vm);
		switch (arrival.Kind)
		{
			case ArrivalKind.Menu when arrival.Key == menu.Key:
				if (vm.AudioStream != audioBefore)
				{
					return new ButtonAction { Type = ButtonActionType.ChangeSetting, Setting = "Audio", Value = vm.AudioStream, Confidence = ActionConfidence.Exact };
				}

				return vm.SpuStream != spuBefore
					? new ButtonAction { Type = ButtonActionType.ChangeSetting, Setting = "Subtitle", Value = vm.SpuStream, Confidence = ActionConfidence.Exact }
					: Unknown("Stays on this menu.");

			case ArrivalKind.Menu:
				var target = this.GetOrAddMenu(vm, arrival, menu, path);
				return target is null
					? Unknown("Opens a menu that was not recorded (menu limit reached).")
					: new ButtonAction { Type = ButtonActionType.OpenMenu, MenuId = target.Node.Id, Confidence = ActionConfidence.Exact };

			case ArrivalKind.Title:
				var follow = this.FollowTitle(vm, arrival.Title, arrival.Part);
				if (follow.Next.Kind == ArrivalKind.Menu)
				{
					// A menu reached after playback: record it so ThenMenuId resolves, but it needs playback to reach.
					var after = this.GetOrAddMenu(vm, follow.Next, menu, [.. path, new PathStep(null, 0, null, AfterPlayback: true)]);
					follow = follow with { ThenMenuId = after?.Node.Id };
				}

				return this.ToPlayAction(follow);

			case ArrivalKind.Stop:
				return Unknown("Playback stops.");

			default:
				return Unknown(arrival.Kind == ArrivalKind.Limit ? "No navigation result within the crawl limits." : "Read error while following the button.");
		}
	}

	/// <summary>Reads until the VM settles on a menu with buttons, enters a title, stops, or a limit is hit.</summary>
	private Arrival RunToArrival(DvdVm vm)
	{
		for (var steps = 0; steps < this._options.MaxStepsPerRun; steps++)
		{
			if (this._clock.Elapsed > this._options.TimeBudget)
			{
				return new Arrival(ArrivalKind.Limit);
			}

			var ev = vm.Step();
			if (ev < 0)
			{
				return new Arrival(ArrivalKind.Error);
			}

			if (ev == DvdNavNative.EventStop)
			{
				return new Arrival(ArrivalKind.Stop);
			}

			if (ev != DvdNavNative.EventNavPacket)
			{
				continue;
			}

			var domain = vm.Domain;
			if (domain == DvdDomain.Title)
			{
				var (title, part) = vm.TitleInfo;
				return new Arrival(ArrivalKind.Title, Title: title, Part: part);
			}

			if (domain is DvdDomain.VideoManagerMenu or DvdDomain.TitleSetMenu)
			{
				var buttons = vm.ReadButtons();
				if (buttons.Count > 0)
				{
					var (_, menuId) = vm.TitleInfo;
					var key = MenuKey(domain, menuId, vm.NavPackLbn, buttons);
					return new Arrival(ArrivalKind.Menu, Key: key, Domain: domain, MenuId: menuId, Buttons: buttons);
				}
			}
		}

		return new Arrival(ArrivalKind.Limit);
	}

	/// <summary>Follows title playback chapter by chapter (skipping inside cells) until it leaves the title.</summary>
	private FollowResult FollowTitle(DvdVm vm, int title, int startPart)
	{
		var endPart = startPart;
		vm.SkipTowardCellEnd();
		for (var steps = 0; steps < this._options.MaxStepsPerRun * 10; steps++)
		{
			if (this._clock.Elapsed > this._options.TimeBudget)
			{
				break;
			}

			var ev = vm.Step();
			if (ev < 0)
			{
				return new FollowResult(title, startPart, endPart, "Unknown", null, null, new Arrival(ArrivalKind.Error));
			}

			if (ev == DvdNavNative.EventStop)
			{
				return new FollowResult(title, startPart, endPart, "Stop", null, null, new Arrival(ArrivalKind.Stop));
			}

			if (ev != DvdNavNative.EventNavPacket)
			{
				continue;
			}

			var domain = vm.Domain;
			if (domain is DvdDomain.VideoManagerMenu or DvdDomain.TitleSetMenu)
			{
				var buttons = vm.ReadButtons();
				if (buttons.Count == 0)
				{
					continue;
				}

				var (_, menuId) = vm.TitleInfo;
				var key = MenuKey(domain, menuId, vm.NavPackLbn, buttons);
				var known = this._menusByKey.GetValueOrDefault(key);
				return new FollowResult(title, startPart, endPart, "ReturnToMenu", known?.Node.Id, null,
					new Arrival(ArrivalKind.Menu, Key: key, Domain: domain, MenuId: menuId, Buttons: buttons));
			}

			if (domain != DvdDomain.Title)
			{
				continue;
			}

			var (t, p) = vm.TitleInfo;
			if (t != title)
			{
				return new FollowResult(title, startPart, endPart, "PlayTitle", null, t, new Arrival(ArrivalKind.Title, Title: t, Part: p));
			}

			if (p < endPart)
			{
				return new FollowResult(title, startPart, endPart, "Loop", null, null, new Arrival(ArrivalKind.Limit));
			}

			endPart = p;
			vm.SkipTowardCellEnd();
		}

		this._map.Complete = false;
		return new FollowResult(title, startPart, endPart, "Unknown", null, null, new Arrival(ArrivalKind.Limit));
	}

	private MenuState? GetOrAddMenu(DvdVm vm, Arrival arrival, MenuState? parent, List<PathStep> path)
	{
		if (this._menusByKey.TryGetValue(arrival.Key!, out var existing))
		{
			// libdvdnav only reports the menu id when the PGC is an entry PGC reached as such.
			if (existing.Node.Kind == MenuKind.Other)
			{
				existing.Node.Kind = MenuKindFor(arrival.MenuId);
			}

			return existing;
		}

		if (this._menus.Count >= this._options.MaxMenus)
		{
			this.MarkIncomplete($"Menu limit ({this._options.MaxMenus}) reached; some menus were not explored.");
			return null;
		}

		if (this._menus.Count == 0)
		{
			this._frame = vm.VideoSize;
		}

		var defaultButton = vm.CurrentButton;
		var node = new MenuNode
		{
			Id = $"m{this._menus.Count}",
			Kind = MenuKindFor(arrival.MenuId),
			Domain = arrival.Domain == DvdDomain.VideoManagerMenu ? "VMGM" : "VTSM",
			ParentMenuId = parent?.Node.Id,
			FrameWidth = this._frame.Width,
			FrameHeight = this._frame.Height,
			DisplayAspectRatio = Math.Round(vm.DisplayAspectRatio, 3),
			Buttons = arrival.Buttons!.Select(b => new MenuButton
			{
				Number = b.Number,
				Rect = new ButtonRect { X = b.X, Y = b.Y, W = b.W, H = b.H },
				IsDefault = b.Number == defaultButton,
				Neighbours = new ButtonNeighbours
				{
					Up = vm.Neighbour(b.Number, NavKey.Up),
					Down = vm.Neighbour(b.Number, NavKey.Down),
					Left = vm.Neighbour(b.Number, NavKey.Left),
					Right = vm.Neighbour(b.Number, NavKey.Right),
				},
			}).ToList(),
		};

		var state = new MenuState(node, arrival.Key!, vm.Duplicate(), path, defaultButton);
		this._menus.Add(state);
		this._menusByKey[state.Key] = state;
		this._map.Menus.Add(node);
		this._queue.Enqueue(state);
		this._log($"menu {node.Id}: {node.Domain} {node.Kind}, {node.Buttons.Count} button(s)");
		return state;
	}

	private ButtonAction ToPlayAction(FollowResult follow)
	{
		var info = this._map.Titles.FirstOrDefault(t => t.DvdTitle == follow.Title);
		var partCount = info?.ChapterCount ?? 0;
		var endKnown = follow.Then is "ReturnToMenu" or "PlayTitle" or "Stop";
		return new ButtonAction
		{
			Type = ButtonActionType.PlayTitle,
			DvdTitle = follow.Title,
			StartChapter = follow.StartPart,
			EndChapter = endKnown ? follow.EndPart : null,
			TitleChapterCount = partCount > 0 ? partCount : null,
			CoversWholeTitle = partCount > 0 && follow.StartPart == 1 && endKnown && follow.EndPart >= partCount,
			Then = follow.Then,
			ThenMenuId = follow.ThenMenuId,
			ThenTitle = follow.ThenTitle,
			Confidence = endKnown ? ActionConfidence.Exact : ActionConfidence.Inferred,
		};
	}

	private void BuildReachPaths()
	{
		var byId = this._menus.ToDictionary(m => m.Node.Id);
		var buttonPaths = this.ShortestButtonPaths();
		foreach (var menu in this._menus)
		{
			if (buttonPaths.TryGetValue(menu.Node.Id, out var viaButtons))
			{
				// Arrow keys + Enter only: renderers (libvlc) cannot send the remote's Title / Root menu keys.
				menu.Node.ReachPath = viaButtons.Keys;
				menu.Node.ParentMenuId = viaButtons.Parent;
				this.SetFocusPaths(menu);
				continue;
			}

			var keys = new List<NavKey>();
			foreach (var step in menu.Path)
			{
				if (step.AfterPlayback)
				{
					keys.Clear();
					break;
				}

				if (step.Key is { } key)
				{
					keys.Add(key);
					continue;
				}

				var from = byId[step.MenuId!];
				var arrows = KeyPathPlanner.Plan(from.Node.Buttons, from.DefaultButton, step.Button);
				if (arrows is null)
				{
					keys.Clear();
					break;
				}

				keys.AddRange(arrows);
				keys.Add(NavKey.Enter);
			}

			menu.Node.ReachPath = keys;
			this.SetFocusPaths(menu);

			if (keys.Count == 0 && menu.Path.Count > 0)
			{
				menu.Node.Screenshot.Error = menu.Path.Any(p => p.AfterPlayback)
					? "Only reachable after playback; no screenshot."
					: "Not reachable with arrow keys from the previous menu; no screenshot.";
			}
		}
	}

	private void SetFocusPaths(MenuState menu)
	{
		foreach (var button in menu.Node.Buttons)
		{
			button.FocusPath = KeyPathPlanner.Plan(menu.Node.Buttons, menu.DefaultButton, button.Number)?.ToList();
		}
	}

	/// <summary>
	/// Breadth-first search from the first menu over "opens menu" buttons: the shortest arrow-key + Enter path to
	/// every menu reachable that way, with the menu it is reached from.
	/// </summary>
	private Dictionary<string, (List<NavKey> Keys, string? Parent)> ShortestButtonPaths()
	{
		var result = new Dictionary<string, (List<NavKey> Keys, string? Parent)>(StringComparer.Ordinal);
		var root = this._menus.FirstOrDefault(m => m.Path.Count == 0);
		if (root is null)
		{
			return result;
		}

		var byId = this._menus.ToDictionary(m => m.Node.Id);
		result[root.Node.Id] = ([], null);
		var queue = new Queue<MenuState>([root]);
		while (queue.Count > 0)
		{
			var from = queue.Dequeue();
			var fromKeys = result[from.Node.Id].Keys;
			foreach (var button in from.Node.Buttons)
			{
				if (button.Action.Type != ButtonActionType.OpenMenu
					|| button.Action.MenuId is not { } targetId
					|| result.ContainsKey(targetId)
					|| !byId.TryGetValue(targetId, out var target))
				{
					continue;
				}

				var arrows = KeyPathPlanner.Plan(from.Node.Buttons, from.DefaultButton, button.Number);
				if (arrows is null)
				{
					continue;
				}

				result[targetId] = ([.. fromKeys, .. arrows, NavKey.Enter], from.Node.Id);
				queue.Enqueue(target);
			}
		}

		return result;
	}

	private void FillReverseIndex()
	{
		foreach (var menu in this._map.Menus)
		{
			foreach (var button in menu.Buttons.Where(b => b.Action.Type == ButtonActionType.PlayTitle))
			{
				this._map.Titles.FirstOrDefault(t => t.DvdTitle == button.Action.DvdTitle)
					?.ReachedFromButtons.Add($"{menu.Id}#{button.Number}");
			}
		}
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

	private static string MenuKey(DvdDomain domain, int menuId, uint lbn, IReadOnlyList<DvdButtonInfo> buttons)
	{
		var hash = new HashCode();
		foreach (var b in buttons)
		{
			hash.Add(b.X);
			hash.Add(b.Y);
			hash.Add(b.W);
			hash.Add(b.H);
		}

		// The menu id is not part of the key: libdvdnav reports it only on some arrivals at the same PGC.
		_ = menuId;
		return $"{domain}:{lbn}:{buttons.Count}:{hash.ToHashCode():x8}";
	}

	private static MenuKind MenuKindFor(int menuId) => menuId switch
	{
		DvdNavNative.MenuTitle => MenuKind.Title,
		DvdNavNative.MenuRoot => MenuKind.Root,
		DvdNavNative.MenuSubpicture => MenuKind.Subtitle,
		DvdNavNative.MenuAudio => MenuKind.Audio,
		DvdNavNative.MenuAngle => MenuKind.Angle,
		DvdNavNative.MenuPart => MenuKind.Chapter,
		_ => MenuKind.Other,
	};

	private static ButtonAction Unknown(string reason) => new() { Type = ButtonActionType.Unknown, Reason = reason };

	private enum ArrivalKind
	{
		Menu,
		Title,
		Stop,
		Limit,
		Error,
	}

	private sealed record Arrival(
		ArrivalKind Kind,
		string? Key = null,
		DvdDomain Domain = DvdDomain.Unknown,
		int MenuId = 0,
		IReadOnlyList<DvdButtonInfo>? Buttons = null,
		int Title = 0,
		int Part = 0);

	private sealed record FollowResult(int Title, int StartPart, int EndPart, string Then, string? ThenMenuId, int? ThenTitle, Arrival Next);

	/// <summary>One step from disc start: a button on a menu, a remote key, or "after playback".</summary>
	private sealed record PathStep(string? MenuId, int Button, NavKey? Key, bool AfterPlayback = false);

	private sealed record MenuState(MenuNode Node, string Key, DvdVm Snapshot, List<PathStep> Path, int DefaultButton);
}
