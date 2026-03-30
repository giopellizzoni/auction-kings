namespace AuctionKings.Application.DTOs;

public sealed record AuctionExtendedNotificationDto(
    Guid AuctionId,
    DateTime NewEndTime);
