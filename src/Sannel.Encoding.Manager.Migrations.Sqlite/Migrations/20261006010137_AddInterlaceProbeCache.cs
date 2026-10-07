using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sannel.Encoding.Manager.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddInterlaceProbeCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InterlaceProbeCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourcePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Playlist = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceSize = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceLastWriteUtc = table.Column<string>(type: "TEXT", nullable: false),
                    Verdict = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    InterlacedPercent = table.Column<double>(type: "REAL", nullable: false),
                    TelecinePercent = table.Column<double>(type: "REAL", nullable: false),
                    SampledFrames = table.Column<int>(type: "INTEGER", nullable: false),
                    HandBrakeDetected = table.Column<bool>(type: "INTEGER", nullable: true),
                    FfmpegVersion = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ProbeVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ProbedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterlaceProbeCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InterlaceProbeCache_SourcePath_Playlist",
                table: "InterlaceProbeCache",
                columns: new[] { "SourcePath", "Playlist" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterlaceProbeCache");
        }
    }
}
