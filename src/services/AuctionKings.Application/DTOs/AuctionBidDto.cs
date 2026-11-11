using AuctionKings.Domain.Enums;

namespace AuctionKings.Application.DTOs;

/// <summary>
/// A bid as seen by a participant looking at an auction's bid list. Bidding is never public:
/// the bidder's identity is deliberately absent, and the caller only learns which bids are
/// their own through <see cref="IsOwnBid"/>.
/// </summary>
public sealed record AuctionBidDto(
    Guid Id,
    decimal Amount,
    string Currency,
    DateTime PlacedAt,
    BidStatus Status,
    bool IsOwnBid);
