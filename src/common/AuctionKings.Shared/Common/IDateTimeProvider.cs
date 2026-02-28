namespace AuctionKings.Shared.Common;

/// <summary>
/// Abstraction over UTC clock. Inject this instead of calling DateTime.UtcNow directly.
/// Enables deterministic testing and explicit server-side time authority.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
