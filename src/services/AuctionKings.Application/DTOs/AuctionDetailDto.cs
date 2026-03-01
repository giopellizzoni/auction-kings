using AuctionKings.Domain.Enums;

namespace AuctionKings.Application.DTOs;

public sealed record AuctionDetailDto(
    Guid Id,
    string Title,
    string Description,
    decimal CurrentPrice,
    string Currency,
    DateTime StartTime,
    DateTime EndTime,
    AuctionStatus Status,
    int BidCount,
    IReadOnlyList<BidDto> TopBids);
