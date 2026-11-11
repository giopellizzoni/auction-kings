using System.Text.Json;
using StackExchange.Redis;

namespace AuctionKings.Infrastructure.Realtime;

/// <summary>
/// Cross-process hand-off for real-time notifications raised outside the API (the background
/// worker has no <c>IHubContext</c>). The worker appends to a Redis Stream; the API consumes it
/// through a consumer group and pushes through SignalR.
/// <para>
/// A stream + consumer group is used instead of pub/sub on purpose: with the SignalR Redis
/// backplane, one <c>SendAsync</c> on any API replica already reaches every client. Pub/sub would
/// make every replica relay the same message (N duplicates); a consumer group delivers each
/// entry to exactly one replica.
/// </para>
/// </summary>
public static class RealtimeNotificationChannel
{
    public const string Key = "realtime:notifications";
    public const string ConsumerGroup = "api";
    public const string PayloadField = "payload";

    /// <summary>Approximate cap so the stream cannot grow unbounded if no API is consuming.</summary>
    public const int MaxLength = 10_000;

    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(RealtimeNotification notification) =>
        JsonSerializer.Serialize(notification, SerializerOptions);

    public static RealtimeNotification? Deserialize(RedisValue payload) =>
        payload.IsNullOrEmpty
            ? null
            : JsonSerializer.Deserialize<RealtimeNotification>((string)payload!, SerializerOptions);
}

public static class RealtimeNotificationType
{
    public const string BidPlaced = "BidPlaced";
    public const string AuctionClosed = "AuctionClosed";
    public const string AuctionExtended = "AuctionExtended";
}

/// <summary>Flat envelope; only the fields relevant to <see cref="Type"/> are set.</summary>
public sealed record RealtimeNotification(
    string Type,
    Guid AuctionId,
    Guid? BidId = null,
    Guid? BidderId = null,
    decimal? Amount = null,
    string? Currency = null,
    Guid? WinnerId = null,
    DateTime? NewEndTime = null);
