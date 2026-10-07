using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sannel.Encoding.Manager.Migrations.Postgres.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourcePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Playlist = table.Column<int>(type: "integer", nullable: false),
                    SourceSize = table.Column<long>(type: "bigint", nullable: false),
                    SourceLastWriteUtc = table.Column<string>(type: "character varying(48)", nullable: false),
                    Verdict = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    InterlacedPercent = table.Column<double>(type: "double precision", nullable: false),
                    TelecinePercent = table.Column<double>(type: "double precision", nullable: false),
                    SampledFrames = table.Column<int>(type: "integer", nullable: false),
                    HandBrakeDetected = table.Column<bool>(type: "boolean", nullable: true),
                    FfmpegVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProbeVersion = table.Column<int>(type: "integer", nullable: false),
                    ProbedAt = table.Column<string>(type: "character varying(48)", nullable: false)
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
