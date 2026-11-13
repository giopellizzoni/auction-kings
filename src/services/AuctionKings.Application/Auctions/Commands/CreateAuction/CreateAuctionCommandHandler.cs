using AuctionKings.Application.Abstractions;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Commands.CreateAuction;

public sealed class CreateAuctionCommandHandler
    : IRequestHandler<CreateAuctionCommand, Result<Guid>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ICacheInvalidationService _cacheInvalidation;

    public CreateAuctionCommandHandler(
        IAuctionRepository auctionRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        ICacheInvalidationService cacheInvalidation
    )
    {
        _auctionRepository = auctionRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _cacheInvalidation = cacheInvalidation;
    }

    public async Task<Result<Guid>> Handle(
        CreateAuctionCommand request,
        CancellationToken cancellationToken
    )
    {
        var money = new Money(request.StartingPrice, request.Currency);
        var duration = new AuctionDuration(request.Duration);
        var now = _dateTimeProvider.UtcNow;
        var startTime = request.StartTime ?? now;

        var created = Auction.Create(
            request.Title,
            request.Description,
            money,
            startTime,
            duration,
            request.SellerId
        );

        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Error);
        }

        var auction = created.Value;

        // Create already activates an auction that starts now or earlier; a future StartTime leaves it Pending
        // until AuctionActivationWorker activates it. Activate is idempotent, so calling it here is harmless,
        // and its result is checked rather than silently dropped.
        var activated = auction.Activate(now);
        if (activated.IsFailure)
        {
            return Result.Failure<Guid>(activated.Error);
        }

        await _auctionRepository.AddAsync(auction, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        // Cache invalidation failure must not prevent the auction from being created (fire-and-forget).
        await _cacheInvalidation.InvalidateActiveListingsAsync(CancellationToken.None);

        return Result.Success(auction.Id);
    }
}
