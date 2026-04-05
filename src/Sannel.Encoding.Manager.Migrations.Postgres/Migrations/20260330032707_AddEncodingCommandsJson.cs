using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sannel.Encoding.Manager.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddEncodingCommandsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncodingCommandsJson",
                table: "EncodeQueueItems",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncodingCommandsJson",
                table: "EncodeQueueItems");
        }
    }
}
