using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionKings.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_unprocessed",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_bids_auction_id_placed_at",
                table: "bids");

            migrationBuilder.DropIndex(
                name: "ix_bids_bidder_id_placed_at",
                table: "bids");

            migrationBuilder.DropIndex(
                name: "ix_auctions_status_end_time",
                table: "auctions");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "auctions");

            migrationBuilder.AddColumn<bool>(
                name: "failed",
                table: "outbox_messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "retry_count",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_keys", x => x.key);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_unprocessed",
                table: "outbox_messages",
                column: "processed_at",
                filter: "processed_at IS NULL AND failed = false");

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_keys_expires_at",
                table: "idempotency_keys",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_keys");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_unprocessed",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "failed",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "retry_count",
                table: "outbox_messages");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "auctions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_unprocessed",
                table: "outbox_messages",
                column: "processed_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_bids_auction_id_placed_at",
                table: "bids",
                columns: new[] { "auction_id", "placed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_bids_bidder_id_placed_at",
                table: "bids",
                columns: new[] { "bidder_id", "placed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_auctions_status_end_time",
                table: "auctions",
                columns: new[] { "status", "end_time" });
        }
    }
}
