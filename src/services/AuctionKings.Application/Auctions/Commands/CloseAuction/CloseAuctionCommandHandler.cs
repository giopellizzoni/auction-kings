using AuctionKings.Application.Abstractions;
using AuctionKings.Domain.Errors;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Commands.CloseAuction;

public sealed class CloseAuctionCommandHandler : IRequestHandler<CloseAuctionCommand, Result>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRealtimeNotificationService _notificationService;
    private readonly ICacheInvalidationService _cacheInvalidation;

    public CloseAuctionCommandHandler(
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

    public async Task<Result> Handle(CloseAuctionCommand request, CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, cancellationToken);

        if (auction is null)
        {
            return Result.Failure(DomainErrors.Auction.NotFound);
        }

        var result = auction.Close(_dateTimeProvider.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await _auctionRepository.UpdateAsync(auction, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        // Notification and cache invalidation failures must not prevent the auction from closing (fire-and-forget).
        await _notificationService.NotifyAuctionClosedAsync(
            auction.Id,
            auction.WinnerId,
            CancellationToken.None);

        await _cacheInvalidation.InvalidateAuctionAsync(auction.Id, CancellationToken.None);
        await _cacheInvalidation.InvalidateActiveListingsAsync(CancellationToken.None);

        return Result.Success();
    }
}
