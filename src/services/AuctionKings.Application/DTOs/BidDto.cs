using AuctionKings.Domain.Enums;

namespace AuctionKings.Application.DTOs;

public sealed record BidDto(
    Guid Id,
    Guid BidderId,
    decimal Amount,
    string Currency,
    DateTime PlacedAt,
    BidStatus Status);
