using AuctionKings.Application.Abstractions;
using AuctionKings.Domain.Aggregates;
using AuctionKings.Domain.ValueObjects;
using AuctionKings.Shared.Common;
using MediatR;

namespace AuctionKings.Application.Auctions.Commands.CreateAuction;

public sealed class CreateAuctionCommandHandler : IRequestHandler<CreateAuctionCommand, Result<Guid>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateAuctionCommandHandler(
        IAuctionRepository auctionRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _auctionRepository = auctionRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> Handle(CreateAuctionCommand request, CancellationToken cancellationToken)
    {
        var money = new Money(request.StartingPrice, request.Currency);
        var duration = new AuctionDuration(request.Duration);
        var now = _dateTimeProvider.UtcNow;

        var auction = Auction.Create(
            request.Title,
            request.Description,
            money,
            now,
            duration,
            request.SellerId);

        await _auctionRepository.AddAsync(auction, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success(auction.Id);
    }
}
