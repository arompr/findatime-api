using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace findatime_api.Migrations;

/// <inheritdoc />
public partial class _20261101173839_AddEventIntervalMinutes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "interval_minutes",
            table: "events",
            type: "integer",
            nullable: false,
            defaultValue: 30);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "interval_minutes",
            table: "events");
    }
}
