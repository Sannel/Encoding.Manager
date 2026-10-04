using Sannel.Encoding.Manager.DiscMenu.Model;
using Sannel.Encoding.Manager.HandBrake;

namespace Sannel.Encoding.Manager.Web.Features.DiscMenu.Services;

/// <summary>Fills in HandBrake title numbers on a disc menu map so the AI can queue what a button plays.</summary>
public static class HandBrakeTitleMapper
{
	/// <summary>
	/// DVD: HandBrake numbers DVD titles by their global title number, so it is copied (and checked against the scan
	/// when one is available). Blu-ray: playlists are matched to <see cref="TitleInfo.Playlist"/> from the scan.
	/// </summary>
	public static void Fill(DiscMenuMap map, IReadOnlyList<TitleInfo>? scannedTitles)
	{
		var isDvd = string.Equals(map.DiscType, "DVD", StringComparison.OrdinalIgnoreCase);
		if (!isDvd && scannedTitles is null)
		{
			map.Warnings.Add("The HandBrake scan is not available, so Blu-ray playlists could not be matched to HandBrake title numbers.");
			return;
		}

		int? Resolve(int? dvdTitle, int? playlist)
		{
			if (isDvd)
			{
				return dvdTitle is { } t && (scannedTitles is null || scannedTitles.Any(s => s.TitleNumber == t)) ? t : null;
			}

			return playlist is { } p ? scannedTitles!.FirstOrDefault(s => s.Playlist == p)?.TitleNumber : null;
		}

		foreach (var title in map.Titles)
		{
			title.HandBrakeTitle = Resolve(title.DvdTitle, title.Playlist);
		}

		foreach (var action in map.Menus.SelectMany(m => m.Buttons).Select(b => b.Action).Append(map.FirstPlay))
		{
			if (action?.Type == ButtonActionType.PlayTitle)
			{
				action.HandBrakeTitle = Resolve(action.DvdTitle, action.Playlist);
			}
		}
	}
}
