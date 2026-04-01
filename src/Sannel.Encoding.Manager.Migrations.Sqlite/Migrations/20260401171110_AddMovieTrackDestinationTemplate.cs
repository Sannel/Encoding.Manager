using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sannel.Encoding.Manager.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddMovieTrackDestinationTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MovieTrackDestinationTemplate",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MovieTrackDestinationTemplate",
                table: "AppSettings");
        }
    }
}
