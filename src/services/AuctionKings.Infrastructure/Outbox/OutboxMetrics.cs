using Prometheus;

namespace AuctionKings.Infrastructure.Outbox;

public static class OutboxMetrics
{
    private static readonly Gauge PendingMessages = Metrics.CreateGauge(
        "auction_kings_outbox_pending_messages_count",
        "Count of outbox messages fetched in the last processing batch (approximation of pending queue depth; capped at batch size)."
    );

    public static void SetPending(int count) => PendingMessages.Set(count);
}
