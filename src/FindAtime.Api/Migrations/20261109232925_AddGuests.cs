using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace findatime_api.Migrations;

/// <inheritdoc />
public partial class _20261109232925_AddGuests : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "guests",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_guests", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_guests_token_hash",
            table: "guests",
            column: "token_hash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "guests");
    }
}
