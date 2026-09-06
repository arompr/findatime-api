using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace findatime_api.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorParticipantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "creator_participant_id",
                table: "events",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE events
                SET creator_participant_id = p.id
                FROM participants p
                WHERE p.event_id = events.id AND p.is_creator = true;
                """);

            migrationBuilder.DropColumn(
                name: "is_creator",
                table: "participants");

            migrationBuilder.Sql(
                """
                WITH new_participants AS (
                    INSERT INTO participants (id, participant_uuid, event_id)
                    SELECT gen_random_uuid(), '00000000-0000-0000-0000-000000000000', e.id
                    FROM events e
                    WHERE e.creator_participant_id IS NULL
                    RETURNING id, event_id
                )
                UPDATE events
                SET creator_participant_id = np.id
                FROM new_participants np
                WHERE np.event_id = events.id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "creator_participant_id",
                table: "events",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_creator",
                table: "participants",
                type: "boolean",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE participants p
                SET is_creator = true
                FROM events e
                WHERE e.id = p.event_id AND e.creator_participant_id = p.id;
                """);

            migrationBuilder.AlterColumn<bool>(
                name: "is_creator",
                table: "participants",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "creator_participant_id",
                table: "events");
        }
    }
}