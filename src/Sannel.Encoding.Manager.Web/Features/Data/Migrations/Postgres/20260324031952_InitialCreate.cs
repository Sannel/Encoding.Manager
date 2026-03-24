using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sannel.Encoding.Manager.Web.Features.Data.Migrations.Postgres
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
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TrackDestinationTemplate = table.Column<string>(type: "text", nullable: false),
                    TrackDestinationRoot = table.Column<string>(type: "text", nullable: true),
                    AudioDefault = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiscScanCache",
                columns: table => new
                {
                    InputPath = table.Column<string>(type: "text", nullable: false),
                    ScanJson = table.Column<string>(type: "text", nullable: false),
                    CachedAt = table.Column<string>(type: "character varying(48)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscScanCache", x => x.InputPath);
                });

            migrationBuilder.CreateTable(
                name: "EncodeQueueItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscPath = table.Column<string>(type: "text", nullable: false),
                    Mode = table.Column<string>(type: "text", nullable: false),
                    TvdbShowName = table.Column<string>(type: "text", nullable: true),
                    AudioDefault = table.Column<string>(type: "text", nullable: false),
                    TracksJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<string>(type: "character varying(48)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncodeQueueItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TvdbEpisodeCache",
                columns: table => new
                {
                    SeriesId = table.Column<int>(type: "integer", nullable: false),
                    OrderType = table.Column<string>(type: "text", nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    EpisodeNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CachedAt = table.Column<string>(type: "character varying(48)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TvdbEpisodeCache", x => new { x.SeriesId, x.OrderType, x.SeasonNumber, x.EpisodeNumber });
                });

            migrationBuilder.CreateTable(
                name: "TvdbSeriesCache",
                columns: table => new
                {
                    SeriesId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    CachedAt = table.Column<string>(type: "character varying(48)", nullable: false)
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
                name: "TvdbEpisodeCache");

            migrationBuilder.DropTable(
                name: "TvdbSeriesCache");
        }
    }
}
