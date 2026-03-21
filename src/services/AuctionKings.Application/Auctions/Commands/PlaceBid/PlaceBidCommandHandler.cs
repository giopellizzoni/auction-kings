using AuctionKings.Application.Abstractions;
using AuctionKings.Application.Exceptions;
using AuctionKings.Domain.Errors;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Commands.PlaceBid;

public sealed class PlaceBidCommandHandler : IRequestHandler<PlaceBidCommand, Result<Guid>>
{
    private const int MaxRetries = 3;

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
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, cancellationToken);

            if (auction is null)
            {
                return Result.Failure<Guid>(DomainErrors.Auction.NotFound);
            }

            var amount = new Money(request.Amount, request.Currency);
            var result = auction.PlaceBid(request.BidderId, amount, _dateTimeProvider.UtcNow);

            if (result.IsFailure)
            {
                return Result.Failure<Guid>(result.Error);
            }

            var bid = result.Value;

            try
            {
                await _auctionRepository.UpdateAsync(auction, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
            }
            catch (ConcurrencyException)
            {
                if (attempt == MaxRetries - 1)
                {
                    return Result.Failure<Guid>(DomainErrors.Auction.ConcurrencyConflict);
                }

                _unitOfWork.ClearTracking();
                continue;
            }

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

        return Result.Failure<Guid>(DomainErrors.Auction.ConcurrencyConflict);
    }
}
