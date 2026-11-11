using AuctionKings.Application.Abstractions;
using AuctionKings.Domain.Errors;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Commands.ActivateAuction;

public sealed class ActivateAuctionCommandHandler : IRequestHandler<ActivateAuctionCommand, Result>
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

    public async Task<Result> Handle(ActivateAuctionCommand request, CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, cancellationToken);

        if (auction is null)
        {
            return Result.Failure(DomainErrors.Auction.NotFound);
        }

        var result = auction.Activate(_dateTimeProvider.UtcNow);

        if (result.IsFailure)
        {
            return result;
        }

        await _auctionRepository.UpdateAsync(auction, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        await _cacheInvalidation.InvalidateActiveListingsAsync(CancellationToken.None);

        return Result.Success();
    }
}
