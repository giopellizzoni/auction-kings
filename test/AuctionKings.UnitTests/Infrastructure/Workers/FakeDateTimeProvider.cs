using AuctionKings.Shared.Common;

namespace AuctionKings.UnitTests.Infrastructure.Workers;

internal sealed class FakeDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; } = utcNow;
}
