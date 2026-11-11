using AuctionKings.Shared.Common;

namespace AuctionKings.Domain.Errors;

public static class DomainErrors
{
    public static class Auction
    {
        public static readonly Error NotActive =
            Error.Validation("Auction.NotActive", "The auction is not active.");

        public static readonly Error AlreadyClosed =
            Error.Validation("Auction.AlreadyClosed", "The auction is already closed.");

        public static readonly Error AlreadyCancelled =
            Error.Validation("Auction.AlreadyCancelled", "The auction has been cancelled.");

        public static readonly Error CannotBidOnOwnAuction =
            Error.Validation("Auction.CannotBidOnOwnAuction", "Sellers cannot place bids on their own auctions.");

        public static readonly Error BidTooLow =
            Error.Validation("Auction.BidTooLow", "The bid amount is below the minimum required increment.");

        public static readonly Error NotFound =
            Error.NotFound("Auction.NotFound", "Auction not found.");

        public static readonly Error InvalidDuration =
            Error.Validation("Auction.InvalidDuration", "The auction duration is invalid.");

        public static readonly Error ConcurrencyConflict =
            Error.Conflict("Auction.ConcurrencyConflict", "The bid could not be placed due to a concurrent update. Please try again.");

        public static readonly Error Ended =
            Error.Validation("Auction.Ended", "The auction has already ended.");

        public static readonly Error NotYetEnded =
            Error.Validation("Auction.NotYetEnded", "The auction has not reached its end time yet.");

        public static readonly Error CannotOutbidSelf =
            Error.Validation("Auction.CannotOutbidSelf", "You are already the highest bidder.");
    }

    public static class Bid
    {
        public static readonly Error InvalidAmount =
            Error.Validation("Bid.InvalidAmount", "The bid amount must be greater than zero.");

        public static readonly Error NotFound =
            Error.NotFound("Bid.NotFound", "Bid not found.");

        public static readonly Error CurrencyMismatch =
            Error.Validation("Bid.CurrencyMismatch", "The bid currency does not match the auction currency.");
    }
}
