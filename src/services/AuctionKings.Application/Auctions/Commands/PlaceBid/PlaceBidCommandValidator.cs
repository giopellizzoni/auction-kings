using FluentValidation;

namespace AuctionKings.Application.Auctions.Commands.PlaceBid;

internal sealed class PlaceBidCommandValidator : AbstractValidator<PlaceBidCommand>
{
    public PlaceBidCommandValidator()
    {
        RuleFor(x => x.AuctionId)
            .NotEmpty();

        RuleFor(x => x.BidderId)
            .NotEmpty();

        // Matches the numeric(18,2) column: a 3rd decimal would be compared in memory at full
        // precision but silently rounded on save.
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3);
    }
}
