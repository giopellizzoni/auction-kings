using AuctionKings.Application.Abstractions;
using AuctionKings.Domain.Errors;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Commands.PlaceBid;

public sealed class PlaceBidCommandHandler : IRequestHandler<PlaceBidCommand, Result<Guid>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRealtimeNotificationService _notificationService;
    private readonly ICacheInvalidationService _cacheInvalidation;

    public PlaceBidCommandHandler(
        IAuctionRepository auctionRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IRealtimeNotificationService notificationService,
        ICacheInvalidationService cacheInvalidation)
    {
        _auctionRepository = auctionRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _notificationService = notificationService;
        _cacheInvalidation = cacheInvalidation;
    }

    public async Task<Result<Guid>> Handle(PlaceBidCommand request, CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, cancellationToken);

        if (auction is null)
        {
            return Result.Failure<Guid>(DomainErrors.Auction.NotFound);
        }

        var amount = new Money(request.Amount, request.Currency);
        var now = _dateTimeProvider.UtcNow;

        var result = auction.PlaceBid(request.BidderId, amount, now);

        if (result.IsFailure)
        {
            return Result.Failure<Guid>(result.Error);
        }

        var bid = result.Value;

        await _auctionRepository.UpdateAsync(auction, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        // Notification and cache invalidation failures must not prevent the bid from succeeding (fire-and-forget).
        await _notificationService.NotifyBidPlacedAsync(
            auction.Id,
            bid.Id,
            bid.BidderId,
            bid.Amount.Amount,
            bid.Amount.Currency,
            CancellationToken.None);

        await _cacheInvalidation.InvalidateAuctionAsync(auction.Id, CancellationToken.None);
        await _cacheInvalidation.InvalidateActiveListingsAsync(CancellationToken.None);

        return Result.Success(bid.Id);
    }
}
