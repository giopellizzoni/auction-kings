using AuctionKings.Infrastructure.Outbox;

namespace AuctionKings.UnitTests.Infrastructure;

public sealed class OutboxMetricsTests
{
    [Fact]
    public void SetPending_DoesNotThrow()
    {
        var exception = Record.Exception(() => OutboxMetrics.SetPending(42));
        Assert.Null(exception);
    }

    [Fact]
    public void SetPending_AcceptsZero()
    {
        var exception = Record.Exception(() => OutboxMetrics.SetPending(0));
        Assert.Null(exception);
    }
}
