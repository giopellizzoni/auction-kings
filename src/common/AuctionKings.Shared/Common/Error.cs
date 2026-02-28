namespace AuctionKings.Shared.Common;

/// <summary>
/// Immutable error descriptor. Use predefined static factories in domain/application layers.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string code, string message) => new(code, message);

    public static Error NotFound(string code, string message) => new(code, message);

    public static Error Conflict(string code, string message) => new(code, message);

    public static Error Unexpected(string code, string message) => new(code, message);

    public override string ToString() => $"[{Code}] {Message}";
}
