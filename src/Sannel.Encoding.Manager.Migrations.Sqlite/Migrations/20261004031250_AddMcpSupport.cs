using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sannel.Encoding.Manager.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "EncodeQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByObjectId",
                table: "EncodeQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedVia",
                table: "EncodeQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscMenuCache",
                columns: table => new
                {
                    InputPath = table.Column<string>(type: "TEXT", nullable: false),
                    DiscType = table.Column<string>(type: "TEXT", nullable: false),
                    MenuJson = table.Column<string>(type: "TEXT", nullable: false),
                    ScreenshotFolder = table.Column<string>(type: "TEXT", nullable: false),
                    ProbeVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    CachedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscMenuCache", x => x.InputPath);
                });

            migrationBuilder.CreateTable(
                name: "UserApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserObjectId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UserDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserPrincipalName = table.Column<string>(type: "TEXT", nullable: true),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    KeyPrefix = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserApiKeys", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserApiKeys_KeyHash",
                table: "UserApiKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserApiKeys_UserObjectId",
                table: "UserApiKeys",
                column: "UserObjectId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscMenuCache");

            migrationBuilder.DropTable(
                name: "UserApiKeys");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "EncodeQueueItems");

            migrationBuilder.DropColumn(
                name: "CreatedByObjectId",
                table: "EncodeQueueItems");

            migrationBuilder.DropColumn(
                name: "CreatedVia",
                table: "EncodeQueueItems");
        }
    }
}
