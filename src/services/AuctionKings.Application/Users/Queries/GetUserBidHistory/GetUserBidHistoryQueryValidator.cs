using FluentValidation;

namespace AuctionKings.Application.Users.Queries.GetUserBidHistory;

internal sealed class GetUserBidHistoryQueryValidator : AbstractValidator<GetUserBidHistoryQuery>
{
    public GetUserBidHistoryQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 50);
    }
}
