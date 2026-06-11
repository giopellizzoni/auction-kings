namespace AuctionKings.API.Filters;

/// <summary>
/// Marks an action as idempotent. <see cref="IdempotencyFilter"/> only enforces
/// the X-Request-Id header and key validation on actions carrying this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute : Attribute { }
