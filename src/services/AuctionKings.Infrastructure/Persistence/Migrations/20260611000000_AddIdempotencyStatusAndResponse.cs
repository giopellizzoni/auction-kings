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
