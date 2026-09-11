using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace findatime_api.Migrations
{
    /// <inheritdoc />
    public partial class AddEventPasscodeAndParticipantGuestIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_participants_event_id",
                table: "participants");

            migrationBuilder.AddColumn<byte[]>(
                name: "passcode_hash",
                table: "events",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "passcode_salt",
                table: "events",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_participants_event_id_guest_id",
                table: "participants",
                columns: new[] { "event_id", "guest_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_participants_event_id_guest_id",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "passcode_hash",
                table: "events");

            migrationBuilder.DropColumn(
                name: "passcode_salt",
                table: "events");

            migrationBuilder.CreateIndex(
                name: "IX_participants_event_id",
                table: "participants",
                column: "event_id");
        }
    }
}
