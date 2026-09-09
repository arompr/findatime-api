using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace findatime_api.Migrations
{
    /// <inheritdoc />
    public partial class PersistDomainEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_participants_event_id",
                table: "participants",
                column: "event_id");

            migrationBuilder.AddForeignKey(
                name: "FK_participants_events_event_id",
                table: "participants",
                column: "event_id",
                principalTable: "events",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_participants_events_event_id",
                table: "participants");

            migrationBuilder.DropIndex(
                name: "IX_participants_event_id",
                table: "participants");
        }
    }
}
