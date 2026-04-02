using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sannel.Encoding.Manager.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentTrackProgressToEncodeQueueItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentTrackProgressPercent",
                table: "EncodeQueueItems",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentTrackProgressPercent",
                table: "EncodeQueueItems");
        }
    }
}
