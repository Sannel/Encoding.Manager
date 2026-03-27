using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sannel.Encoding.Manager.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddRunnerAndLanguageSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompletedAt",
                table: "EncodeQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscRootLabel",
                table: "EncodeQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPercent",
                table: "EncodeQueueItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RunnerName",
                table: "EncodeQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartedAt",
                table: "EncodeQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AudioLanguages",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubtitleLanguages",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Runners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentJobId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runners", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Runners_Name",
                table: "Runners",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Runners");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "EncodeQueueItems");

            migrationBuilder.DropColumn(
                name: "DiscRootLabel",
                table: "EncodeQueueItems");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "EncodeQueueItems");

            migrationBuilder.DropColumn(
                name: "RunnerName",
                table: "EncodeQueueItems");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "EncodeQueueItems");

            migrationBuilder.DropColumn(
                name: "AudioLanguages",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "SubtitleLanguages",
                table: "AppSettings");
        }
    }
}
