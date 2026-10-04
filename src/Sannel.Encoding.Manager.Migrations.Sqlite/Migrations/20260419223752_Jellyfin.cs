using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sannel.Encoding.Manager.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Jellyfin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JellyfinDestRelativePath",
                table: "EncodeQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JellyfinDestRootId",
                table: "EncodeQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JellyfinDestServerId",
                table: "EncodeQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JellyfinSourceItemId",
                table: "EncodeQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JellyfinSourceServerId",
                table: "EncodeQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JellyfinUploadStatus",
                table: "EncodeQueueItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JellyfinServers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", nullable: false),
                    ApiKey = table.Column<string>(type: "TEXT", nullable: false),
                    IsSource = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsDestination = table.Column<bool>(type: "INTEGER", nullable: false),
                    SftpHost = table.Column<string>(type: "TEXT", nullable: true),
                    SftpPort = table.Column<int>(type: "INTEGER", nullable: false),
                    SftpUsername = table.Column<string>(type: "TEXT", nullable: true),
                    SftpPassword = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JellyfinServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JellyfinDestinationRoots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RootPath = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JellyfinDestinationRoots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JellyfinDestinationRoots_JellyfinServers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "JellyfinServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JellyfinSyncProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ServerAId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserIdA = table.Column<string>(type: "TEXT", nullable: false),
                    ServerBId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserIdB = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SyncIntervalMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSyncedAt = table.Column<string>(type: "TEXT", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JellyfinSyncProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JellyfinSyncProfiles_JellyfinServers_ServerAId",
                        column: x => x.ServerAId,
                        principalTable: "JellyfinServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JellyfinSyncProfiles_JellyfinServers_ServerBId",
                        column: x => x.ServerBId,
                        principalTable: "JellyfinServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JellyfinDestinationRoots_ServerId",
                table: "JellyfinDestinationRoots",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_JellyfinSyncProfiles_ServerAId",
                table: "JellyfinSyncProfiles",
                column: "ServerAId");

            migrationBuilder.CreateIndex(
                name: "IX_JellyfinSyncProfiles_ServerBId",
                table: "JellyfinSyncProfiles",
                column: "ServerBId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JellyfinDestinationRoots");

            migrationBuilder.DropTable(
                name: "JellyfinSyncProfiles");

            migrationBuilder.DropTable(
                name: "JellyfinServers");

            migrationBuilder.DropColumn(
                name: "JellyfinDestRelativePath",
                table: "EncodeQueueItems");

            migrationBuilder.DropColumn(
                name: "JellyfinDestRootId",
                table: "EncodeQueueItems");

            migrationBuilder.DropColumn(
                name: "JellyfinDestServerId",
                table: "EncodeQueueItems");

            migrationBuilder.DropColumn(
                name: "JellyfinSourceItemId",
                table: "EncodeQueueItems");

            migrationBuilder.DropColumn(
                name: "JellyfinSourceServerId",
                table: "EncodeQueueItems");

            migrationBuilder.DropColumn(
                name: "JellyfinUploadStatus",
                table: "EncodeQueueItems");
        }
    }
}
