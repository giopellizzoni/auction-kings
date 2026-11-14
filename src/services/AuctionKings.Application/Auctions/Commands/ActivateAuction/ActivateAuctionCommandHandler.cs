using AuctionKings.Application.Abstractions;
using AuctionKings.Domain.Errors;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Commands.ActivateAuction;

public sealed class ActivateAuctionCommandHandler : IRequestHandler<ActivateAuctionCommand, Result<ActivationOutcome>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICacheInvalidationService _cacheInvalidation;

    public ActivateAuctionCommandHandler(
        IAuctionRepository auctionRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        ICacheInvalidationService cacheInvalidation)
    {
        _auctionRepository = auctionRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _cacheInvalidation = cacheInvalidation;
    }

    public async Task<Result<ActivationOutcome>> Handle(ActivateAuctionCommand request, CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, cancellationToken);

        if (auction is null)
        {
            return Result.Failure<ActivationOutcome>(DomainErrors.Auction.NotFound);
        }

        var now = _dateTimeProvider.UtcNow;
        var result = auction.Activate(now);

        if (result.IsFailure && result.Error == DomainErrors.Auction.Ended)
        {
            // The auction ended without ever being activated: cancel it so it leaves the Pending set and the
            // activation worker stops selecting it on every poll.
            var cancelled = auction.Cancel(now);
            if (cancelled.IsFailure)
            {
                return Result.Failure<ActivationOutcome>(cancelled.Error);
            }

            await _auctionRepository.UpdateAsync(auction, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);

            await _cacheInvalidation.InvalidateAuctionAsync(auction.Id, CancellationToken.None);
            await _cacheInvalidation.InvalidateActiveListingsAsync(CancellationToken.None);

            return ActivationOutcome.Cancelled;
        }

        if (result.IsFailure)
        {
            return Result.Failure<ActivationOutcome>(result.Error);
        }

        await _auctionRepository.UpdateAsync(auction, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        await _cacheInvalidation.InvalidateActiveListingsAsync(CancellationToken.None);

        return ActivationOutcome.Activated;
    }
}
