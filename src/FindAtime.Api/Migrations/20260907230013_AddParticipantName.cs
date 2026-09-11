using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace findatime_api.Migrations
{
    /// <inheritdoc />
    public partial class AddParticipantName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "participants",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "name",
                table: "participants");
        }
    }
}
