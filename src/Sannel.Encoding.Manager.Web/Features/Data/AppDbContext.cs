using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Scan.Entities;
using Sannel.Encoding.Manager.Web.Features.Settings.Entities;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Entities;

namespace Sannel.Encoding.Manager.Web.Features.Data;

public class AppDbContext : DbContext
{
	public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
	{
	}

	// SQLite has no native DateTimeOffset type; store as ISO-8601 text globally.
	protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
	{
		configurationBuilder
			.Properties<DateTimeOffset>()
			.HaveConversion<DateTimeOffsetToStringConverter>();

		configurationBuilder
			.Properties<DateTimeOffset?>()
			.HaveConversion<DateTimeOffsetToStringConverter>();
	}

	public DbSet<EncodeQueueItem> EncodeQueueItems => this.Set<EncodeQueueItem>();
	public DbSet<AppSettings> AppSettings => this.Set<AppSettings>();
	public DbSet<DiscScanCache> DiscScanCache => this.Set<DiscScanCache>();
	public DbSet<TvdbSeriesCache> TvdbSeriesCache => this.Set<TvdbSeriesCache>();
	public DbSet<TvdbEpisodeCache> TvdbEpisodeCache => this.Set<TvdbEpisodeCache>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<EncodeQueueItem>(entity =>
		{
			entity.HasKey(e => e.Id);
		});

		modelBuilder.Entity<AppSettings>(entity =>
		{
			entity.HasKey(e => e.Id);
		});

		modelBuilder.Entity<DiscScanCache>(entity =>
		{
			entity.HasKey(e => e.InputPath);
		});

		modelBuilder.Entity<TvdbSeriesCache>(entity =>
		{
			entity.HasKey(e => e.SeriesId);
		});

		modelBuilder.Entity<TvdbEpisodeCache>(entity =>
		{
			entity.HasKey(e => new { e.SeriesId, e.OrderType, e.SeasonNumber, e.EpisodeNumber });
		});
	}
}
