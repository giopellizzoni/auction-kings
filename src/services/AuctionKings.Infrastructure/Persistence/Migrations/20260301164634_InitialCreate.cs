using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionKings.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auctions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    seller_id = table.Column<Guid>(type: "uuid", nullable: false),
                    winner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    current_price_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    current_price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    starting_price_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    starting_price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auctions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bids",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bidder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    placed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    bid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    bid_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bids", x => x.id);
                    table.ForeignKey(
                        name: "fk_bids_auctions_auction_id",
                        column: x => x.auction_id,
                        principalTable: "auctions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auctions_end_time",
                table: "auctions",
                column: "end_time");

            migrationBuilder.CreateIndex(
                name: "ix_auctions_seller_id",
                table: "auctions",
                column: "seller_id");

            migrationBuilder.CreateIndex(
                name: "ix_auctions_status",
                table: "auctions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_bids_auction_id",
                table: "bids",
                column: "auction_id");

            migrationBuilder.CreateIndex(
                name: "ix_bids_auction_id_bidder_id",
                table: "bids",
                columns: new[] { "auction_id", "bidder_id" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_unprocessed",
                table: "outbox_messages",
                column: "processed_at",
                filter: "processed_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bids");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "auctions");
        }
    }
}
