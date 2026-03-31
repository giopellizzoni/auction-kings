namespace AuctionKings.Shared.Common;

/// <summary>
/// Immutable error descriptor. Use predefined static factories in domain/application layers.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.None)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);

    public override string ToString() => $"[{Code}] {Message}";
}
