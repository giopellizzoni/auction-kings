using AuctionKings.Domain.ValueObjects;
using FluentValidation;

namespace AuctionKings.Application.Auctions.Commands.CreateAuction;

public sealed class CreateAuctionCommandValidator : AbstractValidator<CreateAuctionCommand>
{
    public CreateAuctionCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(200);

        RuleFor(x => x.StartingPrice)
            .GreaterThan(0);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Length(3);

        RuleFor(x => x.Duration)
            .Must(d => d >= AuctionDuration.Minimum && d <= AuctionDuration.Maximum)
            .WithMessage(
                $"Duration must be between {AuctionDuration.Minimum.TotalMinutes} minutes and {AuctionDuration.Maximum.TotalDays} days.");

        RuleFor(x => x.SellerId)
            .NotEmpty();
    }
}
