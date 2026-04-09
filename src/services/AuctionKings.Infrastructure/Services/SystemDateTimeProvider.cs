using AuctionKings.Shared.Common;

namespace AuctionKings.Infrastructure.Services;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
