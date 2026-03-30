namespace AuctionKings.Application.DTOs;

public sealed record BidPlacedNotificationDto(
    Guid AuctionId,
    Guid BidId,
    Guid BidderId,
    decimal Amount,
    string Currency);
