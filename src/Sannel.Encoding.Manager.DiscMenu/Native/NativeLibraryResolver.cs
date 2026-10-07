using System.Reflection;
using System.Runtime.InteropServices;

namespace Sannel.Encoding.Manager.DiscMenu.Native;

/// <summary>
/// Maps the logical library names used by the P/Invoke bindings (<c>dvdnav</c>, <c>bluray</c>) to the
/// platform file names, searching a configured folder first and then the OS default search path.
/// </summary>
public static class NativeLibraryResolver
{
	internal const string DvdNav = "dvdnav";
	internal const string Bluray = "bluray";

	private static readonly Dictionary<string, string[]> _linuxNames = new()
	{
		[DvdNav] = ["libdvdnav.so.4", "libdvdnav.so"],
		[Bluray] = ["libbluray.so.4", "libbluray.so.3", "libbluray.so.2", "libbluray.so"],
	};

	private static readonly Dictionary<string, string[]> _windowsNames = new()
	{
		[DvdNav] = ["libdvdnav-4.dll", "libdvdnav.dll", "dvdnav.dll"],
		// libbluray 1.5 is ABI 4; the structures read here only gained trailing fields since ABI 2.
		[Bluray] = ["libbluray-4.dll", "libbluray-3.dll", "libbluray-2.dll", "libbluray.dll", "bluray.dll"],
	};

	private static readonly Dictionary<string, string[]> _macNames = new()
	{
		[DvdNav] = ["libdvdnav.4.dylib", "libdvdnav.dylib"],
		[Bluray] = ["libbluray.4.dylib", "libbluray.3.dylib", "libbluray.2.dylib", "libbluray.dylib"],
	};

	private static string? _searchPath;
	private static int _installed;

	/// <summary>Installs the resolver for this assembly. Safe to call more than once.</summary>
	/// <param name="searchPath">Optional folder searched before the OS default search path.</param>
	public static void Install(string? searchPath)
	{
		_searchPath = string.IsNullOrWhiteSpace(searchPath) ? null : searchPath;
		if (Interlocked.Exchange(ref _installed, 1) == 0)
		{
			NativeLibrary.SetDllImportResolver(typeof(NativeLibraryResolver).Assembly, Resolve);
		}
	}

	/// <summary>Tries to load a library, returning null on success or the reason it could not be loaded.</summary>
	public static string? Probe(string logicalName)
	{
		var handle = Load(logicalName);
		return handle == IntPtr.Zero
			? $"lib{logicalName} could not be loaded (tried {string.Join(", ", Candidates(logicalName))}{(_searchPath is null ? string.Empty : $" in {_searchPath} and")} the system library path)"
			: null;
	}

	private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
		libraryName is DvdNav or Bluray ? Load(libraryName) : IntPtr.Zero;

	private static IntPtr Load(string logicalName)
	{
		foreach (var name in Candidates(logicalName))
		{
			if (_searchPath is not null
				&& NativeLibrary.TryLoad(Path.Combine(_searchPath, name), out var fromFolder))
			{
				return fromFolder;
			}

			if (NativeLibrary.TryLoad(name, out var handle))
			{
				return handle;
			}
		}

		return IntPtr.Zero;
	}

	private static string[] Candidates(string logicalName)
	{
		var table = OperatingSystem.IsWindows() ? _windowsNames
			: OperatingSystem.IsMacOS() ? _macNames
			: _linuxNames;
		return table.TryGetValue(logicalName, out var names) ? names : [logicalName];
	}
}
