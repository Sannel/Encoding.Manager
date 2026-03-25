using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sannel.Encoding.Manager.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrackDestinationTemplate = table.Column<string>(type: "TEXT", nullable: false),
                    TrackDestinationRoot = table.Column<string>(type: "TEXT", nullable: true),
                    AudioDefault = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscScanCache",
                columns: table => new
                {
                    InputPath = table.Column<string>(type: "TEXT", nullable: false),
                    ScanJson = table.Column<string>(type: "TEXT", nullable: false),
                    CachedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscScanCache", x => x.InputPath);
                });

            migrationBuilder.CreateTable(
                name: "EncodeQueueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DiscPath = table.Column<string>(type: "TEXT", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", nullable: false),
                    TvdbShowName = table.Column<string>(type: "TEXT", nullable: true),
                    AudioDefault = table.Column<string>(type: "TEXT", nullable: false),
                    TracksJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncodeQueueItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EncodingPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    RootLabel = table.Column<string>(type: "TEXT", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncodingPresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TvdbEpisodeCache",
                columns: table => new
                {
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderType = table.Column<string>(type: "TEXT", nullable: false),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    EpisodeNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CachedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvdbEpisodeCache", x => new { x.SeriesId, x.OrderType, x.SeasonNumber, x.EpisodeNumber });
                });

            migrationBuilder.CreateTable(
                name: "TvdbSeriesCache",
                columns: table => new
                {
                    SeriesId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    CachedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvdbSeriesCache", x => x.SeriesId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "DiscScanCache");

            migrationBuilder.DropTable(
                name: "EncodeQueueItems");

            migrationBuilder.DropTable(
                name: "EncodingPresets");

            migrationBuilder.DropTable(
                name: "TvdbEpisodeCache");

            migrationBuilder.DropTable(
                name: "TvdbSeriesCache");
        }
    }
}
