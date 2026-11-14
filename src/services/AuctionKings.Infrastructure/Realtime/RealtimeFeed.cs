using System.Text.Json;

namespace AuctionKings.Infrastructure.Realtime;

/// <summary>
/// Wire contract of the Redis Stream that carries real-time notifications from processes that cannot push
/// to SignalR clients themselves (the background worker) to the API, which owns the hubs.
/// </summary>
public static class RealtimeFeed
{
    /// <summary>The stream key. Both sides must agree on it.</summary>
    public const string Key = "realtime:notifications";

    /// <summary>Consumer group of the API replicas: each entry is handed to exactly one replica.</summary>
    public const string Group = "api";

    /// <summary>The single field of a stream entry; it holds a serialized <see cref="RealtimeEnvelope"/>.</summary>
    public const string Field = "msg";

    /// <summary>Incremented when the envelope or a payload changes incompatibly. Readers drop newer versions.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Approximate cap on the stream length, so an absent API cannot grow Redis without bound.</summary>
    public const int MaxLength = 10_000;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize<TPayload>(string kind, DateTime occurredAt, TPayload payload) =>
        JsonSerializer.Serialize(
            new RealtimeEnvelope(
                CurrentVersion,
                kind,
                occurredAt,
                JsonSerializer.SerializeToElement(payload, SerializerOptions)
            ),
            SerializerOptions
        );

    /// <summary>Parses an entry, or returns null when it is not a well-formed envelope.</summary>
    public static RealtimeEnvelope? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RealtimeEnvelope>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads the payload of an envelope, or returns null when it does not match <typeparamref name="TPayload"/>.</summary>
    public static TPayload? TryReadPayload<TPayload>(RealtimeEnvelope envelope)
        where TPayload : class
    {
        // A missing property leaves a default (Undefined) element, and Deserialize on it throws
        // InvalidOperationException rather than JsonException.
        if (envelope.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        try
        {
            return envelope.Payload.Deserialize<TPayload>(SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The kinds of notification the stream carries.</summary>
public static class RealtimeKinds
{
    public const string BidPlaced = "BidPlaced";
    public const string AuctionClosed = "AuctionClosed";
    public const string AuctionExtended = "AuctionExtended";
    public const string AuctionCancelled = "AuctionCancelled";
}

public sealed record RealtimeEnvelope(int Version, string Kind, DateTime OccurredAt, JsonElement Payload);

public sealed record BidPlacedPayload(Guid AuctionId, Guid BidId, decimal Amount, string Currency);

public sealed record AuctionClosedPayload(Guid AuctionId, Guid? WinnerId);

public sealed record AuctionExtendedPayload(Guid AuctionId, DateTime NewEndTime);

public sealed record AuctionCancelledPayload(Guid SellerId, Guid AuctionId, string Title, DateTime CancelledAt);
