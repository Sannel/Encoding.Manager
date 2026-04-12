using Microsoft.EntityFrameworkCore;
using Sannel.Encoding.Manager.Web.Features.Data;
using Sannel.Encoding.Manager.Web.Features.Settings.Entities;
using Sannel.Encoding.Manager.Web.Features.Shared.Services;

namespace Sannel.Encoding.Manager.Web.Features.Settings.Services;

public class SettingsService : ISettingsService
{
	private readonly IDbContextFactory<AppDbContext> _dbFactory;

	public SettingsService(IDbContextFactory<AppDbContext> dbFactory)
	{
		_dbFactory = dbFactory;
	}

	/// <inheritdoc />
	public async Task<AppSettings> GetSettingsAsync(CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		return await ctx.AppSettings.FirstOrDefaultAsync(ct).ConfigureAwait(false)
			?? new AppSettings();
	}

	/// <inheritdoc />
	public async Task SaveSettingsAsync(AppSettings settings, CancellationToken ct = default)
	{
		await using var ctx = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
		var existing = await ctx.AppSettings.FirstOrDefaultAsync(ct).ConfigureAwait(false);
		if (existing is null)
		{
			settings.Id = 1;
			settings.TrackDestinationTemplate = PathHelper.ToForwardSlash(settings.TrackDestinationTemplate);
			settings.MovieTrackDestinationTemplate = PathHelper.ToForwardSlash(settings.MovieTrackDestinationTemplate);
			ctx.AppSettings.Add(settings);
		}
		else
		{
			existing.TrackDestinationRoot = settings.TrackDestinationRoot;
			existing.TrackDestinationTemplate = PathHelper.ToForwardSlash(settings.TrackDestinationTemplate);
			existing.MovieTrackDestinationTemplate = PathHelper.ToForwardSlash(settings.MovieTrackDestinationTemplate);
			existing.AudioDefault = settings.AudioDefault;
			existing.AudioLanguages = settings.AudioLanguages;
			existing.SubtitleLanguages = settings.SubtitleLanguages;
			existing.TvdbLanguage = settings.TvdbLanguage;
		}

		await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
	}
}
