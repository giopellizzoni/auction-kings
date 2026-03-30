namespace AuctionKings.Application.DTOs;

public sealed record AuctionClosedNotificationDto(
    Guid AuctionId,
    Guid? WinnerId);
