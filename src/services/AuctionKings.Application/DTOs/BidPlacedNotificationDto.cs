namespace AuctionKings.Application.DTOs;

public sealed record BidPlacedNotificationDto(
    Guid AuctionId,
    Guid BidId,
    decimal Amount,
    string Currency);
