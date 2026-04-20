using System.Text.RegularExpressions;
using Sannel.Encoding.Manager.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public partial class JellyfinPathBuilder : IJellyfinPathBuilder
{
	public string BuildRemotePath(JellyfinDestinationRoot root, JellyfinItem item, string extension = "mkv")
	{
		var rootPath = root.RootPath.TrimEnd('/');

		if (string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase))
		{
			return this.BuildEpisodePath(rootPath, item, extension);
		}

		if (string.Equals(item.Type, "Movie", StringComparison.OrdinalIgnoreCase))
		{
			return this.BuildMoviePath(rootPath, item, extension);
		}

		throw new ArgumentException($"Unsupported item type: {item.Type}");
	}

	private string BuildEpisodePath(string rootPath, JellyfinItem item, string extension)
	{
		var seriesName = Sanitize(item.SeriesName ?? "Unknown Series");
		var seasonNumber = item.ParentIndexNumber ?? 0;
		var episodeNumber = item.IndexNumber ?? 0;
		var episodeTitle = Sanitize(item.Name);
		var providerTag = BuildProviderTag(item.SeriesProviderIds);

		var seriesFolder = string.IsNullOrEmpty(providerTag)
			? seriesName
			: $"{seriesName} {providerTag}";

		var seasonFolder = $"Season {seasonNumber:D2}";
		var fileName = $"{seriesName} S{seasonNumber:D2}E{episodeNumber:D2} - {episodeTitle}.{extension}";

		return $"{rootPath}/{seriesFolder}/{seasonFolder}/{fileName}";
	}

	private string BuildMoviePath(string rootPath, JellyfinItem item, string extension)
	{
		var title = Sanitize(item.Name);
		var year = item.ProductionYear;
		var providerTag = BuildProviderTag(item.ProviderIds);

		var nameWithYear = year.HasValue ? $"{title} ({year})" : title;
		var folder = string.IsNullOrEmpty(providerTag)
			? nameWithYear
			: $"{nameWithYear} {providerTag}";

		var fileName = $"{nameWithYear}.{extension}";

		return $"{rootPath}/{folder}/{fileName}";
	}

	private static string BuildProviderTag(JellyfinProviderIds? providerIds)
	{
		if (providerIds is null)
		{
			return string.Empty;
		}

		if (!string.IsNullOrEmpty(providerIds.Tvdb))
		{
			return $"[tvdbid-{providerIds.Tvdb}]";
		}

		if (!string.IsNullOrEmpty(providerIds.Tmdb))
		{
			return $"[tmdbid-{providerIds.Tmdb}]";
		}

		if (!string.IsNullOrEmpty(providerIds.Imdb))
		{
			return $"[imdbid-{providerIds.Imdb}]";
		}

		return string.Empty;
	}

	private static string Sanitize(string name) =>
		IllegalCharsRegex().Replace(name, string.Empty).Trim();

	[GeneratedRegex(@"[<>:""/\\|?*]")]
	private static partial Regex IllegalCharsRegex();
}
