using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuctionKings.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds composite indexes on hot query paths identified by load testing.
    ///
    /// Covered queries:
    ///   - GetActiveAuctions / GetExpiredAuctions: WHERE status = Active ORDER BY end_time
    ///     → ix_auctions_status_end_time (status, end_time)
    ///
    ///   - GetAuctionBids (paged): WHERE auction_id = ? ORDER BY placed_at DESC
    ///     → ix_bids_auction_id_placed_at (auction_id, placed_at)
    ///       eliminates filesort on top of the existing single-column ix_bids_auction_id
    ///
    ///   - GetUserBidHistory (paged): WHERE bidder_id = ? ORDER BY placed_at DESC
    ///     → ix_bids_bidder_id_placed_at (bidder_id, placed_at)
    ///       new covering index; previously had no index on bidder_id alone
    /// </summary>
    public partial class AddPerformanceIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_auctions_status_end_time",
                table: "auctions",
                columns: new[] { "status", "end_time" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_bids_auction_id_placed_at",
                table: "bids",
                columns: new[] { "auction_id", "placed_at" }
            );

            migrationBuilder.CreateIndex(
                name: "ix_bids_bidder_id_placed_at",
                table: "bids",
                columns: new[] { "bidder_id", "placed_at" }
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_auctions_status_end_time", table: "auctions");
            migrationBuilder.DropIndex(name: "ix_bids_auction_id_placed_at", table: "bids");
            migrationBuilder.DropIndex(name: "ix_bids_bidder_id_placed_at", table: "bids");
        }
    }
}
