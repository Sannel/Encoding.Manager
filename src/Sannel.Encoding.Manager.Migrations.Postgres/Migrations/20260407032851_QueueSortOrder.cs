using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sannel.Encoding.Manager.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class QueueSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "EncodeQueueItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: assign sequential SortOrder based on CreatedAt ascending
            migrationBuilder.Sql(
                """
                UPDATE "EncodeQueueItems"
                SET "SortOrder" = sub.rn - 1
                FROM (
                    SELECT "Id", ROW_NUMBER() OVER (ORDER BY "CreatedAt") - 1 AS rn
                    FROM "EncodeQueueItems"
                ) sub
                WHERE "EncodeQueueItems"."Id" = sub."Id"
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "EncodeQueueItems");
        }
    }
}
