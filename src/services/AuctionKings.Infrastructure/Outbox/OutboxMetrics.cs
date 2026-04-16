using Prometheus;

namespace AuctionKings.Infrastructure.Outbox;

public static class OutboxMetrics
{
    private static readonly Gauge PendingMessages = Metrics.CreateGauge(
        "auction_kings_outbox_pending_messages",
        "Number of unprocessed outbox messages."
    );

    public static void SetPending(int count) => PendingMessages.Set(count);
}
