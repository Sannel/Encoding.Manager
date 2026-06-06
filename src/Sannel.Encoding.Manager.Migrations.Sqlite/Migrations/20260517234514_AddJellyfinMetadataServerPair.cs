using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sannel.Encoding.Manager.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddJellyfinMetadataServerPair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JellyfinMetadataServerPairs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DestinationServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSyncedAt = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JellyfinMetadataServerPairs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JellyfinMetadataServerPairs_JellyfinServers_DestinationServerId",
                        column: x => x.DestinationServerId,
                        principalTable: "JellyfinServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JellyfinMetadataServerPairs_JellyfinServers_SourceServerId",
                        column: x => x.SourceServerId,
                        principalTable: "JellyfinServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JellyfinMetadataServerPairs_DestinationServerId",
                table: "JellyfinMetadataServerPairs",
                column: "DestinationServerId");

            migrationBuilder.CreateIndex(
                name: "IX_JellyfinMetadataServerPairs_SourceServerId",
                table: "JellyfinMetadataServerPairs",
                column: "SourceServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JellyfinMetadataServerPairs");
        }
    }
}
