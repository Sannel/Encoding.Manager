using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sannel.Encoding.Manager.Migrations.Sqlite.Migrations
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
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Backfill: assign sequential SortOrder based on CreatedAt ascending
            migrationBuilder.Sql(
                """
                UPDATE EncodeQueueItems
                SET SortOrder = (
                    SELECT COUNT(*)
                    FROM EncodeQueueItems e2
                    WHERE e2.CreatedAt < EncodeQueueItems.CreatedAt
                )
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
