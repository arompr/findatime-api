using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace findatime_api.Migrations
{
    /// <inheritdoc />
    public partial class RenameParticipantUuidToGuestId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "participant_uuid",
                table: "participants",
                newName: "guest_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "guest_id",
                table: "participants",
                newName: "participant_uuid");
        }
    }
}
