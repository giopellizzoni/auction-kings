namespace AuctionKings.Application.Abstractions;

public interface IPasswordHasher
{
    /// <summary>Returns a self-describing hash (algorithm, work factor, salt and digest) safe to store.</summary>
    string Hash(string password);

    /// <summary>
    /// Checks <paramref name="password"/> against a stored hash in constant time. A malformed hash is a mismatch,
    /// never an exception. A null hash still costs one full derivation, so a caller that found no account can
    /// pass null and take as long as for a real one.
    /// </summary>
    bool Verify(string password, string? passwordHash);
}
