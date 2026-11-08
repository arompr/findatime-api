using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace findatime_api.Migrations;

/// <inheritdoc />
public partial class _20261108220129_AddEventDayWindow : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "day_end_hour",
            table: "events",
            type: "integer",
            nullable: false,
            defaultValue: 21);

        migrationBuilder.AddColumn<int>(
            name: "day_start_hour",
            table: "events",
            type: "integer",
            nullable: false,
            defaultValue: 9);

        migrationBuilder.AddCheckConstraint(
            name: "ck_events_day_window",
            table: "events",
            sql: "\"day_start_hour\" >= 0 AND \"day_end_hour\" <= 24 AND \"day_start_hour\" < \"day_end_hour\"");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_events_day_window",
            table: "events");

        migrationBuilder.DropColumn(
            name: "day_end_hour",
            table: "events");

        migrationBuilder.DropColumn(
            name: "day_start_hour",
            table: "events");
    }
}
