using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace findatime_api.Migrations
{
    /// <inheritdoc />
    public partial class AddAvailabilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "timezone",
                table: "participants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "timezone",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "availabilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_availabilities", x => x.id);
                    table.CheckConstraint("ck_availabilities_end_after_start", "\"end_utc\" > \"start_utc\"");
                    table.CheckConstraint("ck_availabilities_max_duration", "\"end_utc\" - \"start_utc\" <= interval '24 hours'");
                    table.ForeignKey(
                        name: "FK_availabilities_participants_participant_id",
                        column: x => x.participant_id,
                        principalTable: "participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_availabilities_participant_id",
                table: "availabilities",
                column: "participant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "availabilities");

            migrationBuilder.DropColumn(
                name: "timezone",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "timezone",
                table: "events");
        }
    }
}
