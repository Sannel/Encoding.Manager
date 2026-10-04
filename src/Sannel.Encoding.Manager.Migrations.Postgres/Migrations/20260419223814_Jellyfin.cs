using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sannel.Encoding.Manager.Migrations.Postgres.Migrations
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
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JellyfinDestRootId",
                table: "EncodeQueueItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JellyfinDestServerId",
                table: "EncodeQueueItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JellyfinSourceItemId",
                table: "EncodeQueueItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JellyfinSourceServerId",
                table: "EncodeQueueItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JellyfinUploadStatus",
                table: "EncodeQueueItems",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JellyfinServers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    BaseUrl = table.Column<string>(type: "text", nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: false),
                    IsSource = table.Column<bool>(type: "boolean", nullable: false),
                    IsDestination = table.Column<bool>(type: "boolean", nullable: false),
                    SftpHost = table.Column<string>(type: "text", nullable: true),
                    SftpPort = table.Column<int>(type: "integer", nullable: false),
                    SftpUsername = table.Column<string>(type: "text", nullable: true),
                    SftpPassword = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<string>(type: "character varying(48)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JellyfinServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JellyfinDestinationRoots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ServerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootPath = table.Column<string>(type: "text", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ServerAId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserIdA = table.Column<string>(type: "text", nullable: false),
                    ServerBId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserIdB = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SyncIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    LastSyncedAt = table.Column<string>(type: "character varying(48)", nullable: true),
                    LastSyncStatus = table.Column<string>(type: "text", nullable: true)
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
