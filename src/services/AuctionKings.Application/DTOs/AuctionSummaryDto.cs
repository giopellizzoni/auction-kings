using AuctionKings.Domain.Enums;

namespace AuctionKings.Application.DTOs;

public sealed record AuctionSummaryDto(
    Guid Id,
    string Title,
    decimal CurrentPrice,
    string Currency,
    DateTime EndTime,
    AuctionStatus Status,
    int BidCount);
