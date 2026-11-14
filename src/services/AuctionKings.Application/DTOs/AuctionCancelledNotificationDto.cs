namespace AuctionKings.Application.DTOs;

/// <summary>Sent only to the seller. Deliberately carries no bidder information.</summary>
public sealed record AuctionCancelledNotificationDto(
    Guid AuctionId,
    string Title,
    DateTime CancelledAt);
