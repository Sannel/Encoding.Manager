using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sannel.Encoding.Manager.Web.Features.Jellyfin.Entities;
using Sannel.Encoding.Manager.Web.Features.Logging.Entities;
using Sannel.Encoding.Manager.Web.Features.Queue.Entities;
using Sannel.Encoding.Manager.Web.Features.Scan.Entities;
using Sannel.Encoding.Manager.Web.Features.Settings.Entities;
using Sannel.Encoding.Manager.Web.Features.Tvdb.Entities;
using RunnerEntity = Sannel.Encoding.Manager.Web.Features.Runner.Entities.Runner;

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
	public DbSet<EncodingPreset> EncodingPresets => this.Set<EncodingPreset>();
	public DbSet<AppSettings> AppSettings => this.Set<AppSettings>();
	public DbSet<DiscScanCache> DiscScanCache => this.Set<DiscScanCache>();
	public DbSet<TvdbSeriesCache> TvdbSeriesCache => this.Set<TvdbSeriesCache>();
	public DbSet<TvdbEpisodeCache> TvdbEpisodeCache => this.Set<TvdbEpisodeCache>();
	public DbSet<RunnerEntity> Runners => this.Set<RunnerEntity>();
	public DbSet<JellyfinServer> JellyfinServers => this.Set<JellyfinServer>();
	public DbSet<JellyfinSyncProfile> JellyfinSyncProfiles => this.Set<JellyfinSyncProfile>();
	public DbSet<JellyfinDestinationRoot> JellyfinDestinationRoots => this.Set<JellyfinDestinationRoot>();
	public DbSet<LogEntry> LogEntries => this.Set<LogEntry>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<EncodeQueueItem>(entity =>
		{
			entity.HasKey(e => e.Id);
		});

		modelBuilder.Entity<EncodingPreset>(entity =>
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

		modelBuilder.Entity<RunnerEntity>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => e.Name).IsUnique();
		});

		modelBuilder.Entity<JellyfinServer>(entity =>
		{
			entity.HasKey(e => e.Id);
		});

		modelBuilder.Entity<JellyfinSyncProfile>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.HasOne(e => e.ServerA)
				.WithMany()
				.HasForeignKey(e => e.ServerAId)
				.OnDelete(DeleteBehavior.Restrict);
			entity.HasOne(e => e.ServerB)
				.WithMany()
				.HasForeignKey(e => e.ServerBId)
				.OnDelete(DeleteBehavior.Restrict);
		});

		modelBuilder.Entity<JellyfinDestinationRoot>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.HasOne(e => e.Server)
				.WithMany()
				.HasForeignKey(e => e.ServerId)
				.OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<LogEntry>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => e.Timestamp);
			entity.HasIndex(e => e.Level);
			entity.HasIndex(e => e.Source);
		});
	}
}
