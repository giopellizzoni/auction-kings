using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionKings.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyStatusAndResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "idempotency_keys",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Reserved");

            migrationBuilder.AddColumn<string>(
                name: "response",
                table: "idempotency_keys",
                type: "text",
                nullable: true);

            // Rows that exist before this migration predate the status column and belong to commands
            // that were already executed. Left as the column default ('Reserved') they would look like
            // abandoned reservations, and stale-reservation reclaim/cleanup would delete them — letting a
            // retry with the same key re-execute (e.g. place a duplicate bid) within its 24h window.
            // As 'Completed' without a stored response they keep answering 409 until they expire.
            migrationBuilder.Sql("UPDATE idempotency_keys SET status = 'Completed';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status",
                table: "idempotency_keys");

            migrationBuilder.DropColumn(
                name: "response",
                table: "idempotency_keys");
        }
    }
}
