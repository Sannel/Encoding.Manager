using System.Text.RegularExpressions;
using Sannel.Encoding.Manager.Jellyfin.Dto;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Jellyfin.Services;

public partial class JellyfinPathBuilder : IJellyfinPathBuilder
{
	public string BuildRemotePath(JellyfinDestinationRoot root, JellyfinItem item, string extension = "mkv")
	{
		if (string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase))
		{
			return this.BuildEpisodePath(item, extension);
		}

		if (string.Equals(item.Type, "Movie", StringComparison.OrdinalIgnoreCase))
		{
			return this.BuildMoviePath(item, extension);
		}

		throw new ArgumentException($"Unsupported item type: {item.Type}");
	}

	private string BuildEpisodePath(JellyfinItem item, string extension)
	{
		var seriesName = Sanitize(item.SeriesName ?? "Unknown Series");
		var seasonNumber = item.ParentIndexNumber ?? 0;
		var episodeNumber = item.IndexNumber ?? 0;
		var episodeTitle = Sanitize(item.Name);
		var tvdbId = ExtractTvdbIdFromPath(item.Path, levelsAboveFile: 2);
		var providerTag = string.IsNullOrEmpty(tvdbId) ? string.Empty : $"[tvdbid-{tvdbId}]";

		var seriesFolder = string.IsNullOrEmpty(providerTag)
			? seriesName
			: $"{seriesName} {providerTag}";

		var seasonFolder = $"Season {seasonNumber:D2}";
		var fileName = $"s{seasonNumber:D2}e{episodeNumber:D2} - {episodeTitle}.{extension}";

		return $"{seriesFolder}/{seasonFolder}/{fileName}";
	}

	private string BuildMoviePath(JellyfinItem item, string extension)
	{
		var title = Sanitize(item.Name);
		var year = item.ProductionYear;
		var tvdbId = ExtractTvdbIdFromPath(item.Path, levelsAboveFile: 1);
		var providerTag = string.IsNullOrEmpty(tvdbId) ? string.Empty : $"[tvdbid-{tvdbId}]";

		var nameWithYear = year.HasValue ? $"{title} ({year})" : title;
		var folder = string.IsNullOrEmpty(providerTag)
			? nameWithYear
			: $"{nameWithYear} {providerTag}";

		var fileName = $"{nameWithYear}.{extension}";

		return $"{folder}/{fileName}";
	}

	public int? ExtractTvdbId(JellyfinItem item)
	{
		var isEpisode = string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase);
		var levelsAboveFile = isEpisode ? 2 : 1;
		var fromPath = ExtractTvdbIdFromPath(item.Path, levelsAboveFile);
		if (int.TryParse(fromPath, out var tvdbId))
		{
			return tvdbId;
		}

		var tvdbString = isEpisode
			? item.SeriesProviderIds?.Tvdb ?? item.ProviderIds?.Tvdb
			: item.ProviderIds?.Tvdb;
		return int.TryParse(tvdbString, out var parsed) ? parsed : null;
	}

	private static string? ExtractTvdbIdFromPath(string? filePath, int levelsAboveFile)
	{
		if (string.IsNullOrEmpty(filePath))
		{
			return null;
		}

		var segments = filePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
		var folderIndex = segments.Length - 1 - levelsAboveFile;
		if (folderIndex < 0)
		{
			return null;
		}

		var match = TvdbIdFolderRegex().Match(segments[folderIndex]);
		return match.Success ? match.Groups[1].Value : null;
	}

	private static string Sanitize(string name) =>
		IllegalCharsRegex().Replace(name, string.Empty).Trim();

	[GeneratedRegex(@"[<>:""/\\|?*]")]
	private static partial Regex IllegalCharsRegex();
	[GeneratedRegex(@"\[tvdbid-(\d+)\]", RegexOptions.IgnoreCase)]
	private static partial Regex TvdbIdFolderRegex();}
