using FluentValidation;

namespace AuctionKings.Application.Auctions.Commands.CloseAuction;

internal sealed class CloseAuctionCommandValidator : AbstractValidator<CloseAuctionCommand>
{
    public CloseAuctionCommandValidator()
    {
        RuleFor(x => x.AuctionId)
            .NotEmpty();
    }
}
