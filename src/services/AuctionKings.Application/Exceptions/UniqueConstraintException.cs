namespace AuctionKings.Application.Exceptions;

/// <summary>
/// A commit hit a unique index — typically two requests creating the same natural key at once, after both
/// passed an existence check.
/// </summary>
public sealed class UniqueConstraintException(string? constraintName, Exception? innerException = null)
    : Exception($"A unique constraint was violated ({constraintName ?? "unknown"}).", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
