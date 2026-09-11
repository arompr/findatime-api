using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace findatime_api.Migrations
{
    /// <inheritdoc />
    public partial class AddEventPublicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "public_id",
                table: "events",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE events AS e
                SET public_id = (
                    SELECT string_agg(
                        substr('0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ',
                               (floor(random() * 62)::int + 1), 1), '')
                    FROM generate_series(1, 12)
                    WHERE e.id IS NOT NULL
                )
                WHERE e.public_id IS NULL;
                """);

            migrationBuilder.Sql("ALTER TABLE \"events\" ALTER COLUMN \"public_id\" SET NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_events_public_id",
                table: "events",
                column: "public_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_events_public_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "public_id",
                table: "events");
        }
    }
}
