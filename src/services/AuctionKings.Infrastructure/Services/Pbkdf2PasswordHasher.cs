using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AuctionKings.Application.Abstractions;

namespace AuctionKings.Infrastructure.Services;

/// <summary>
/// PBKDF2-HMAC-SHA512 with a random 16-byte salt and a 32-byte digest, stored as
/// <c>pbkdf2-sha512.{iterations}.{salt}.{digest}</c> (Base64). The iteration count travels with the hash, so it
/// can be raised later without breaking existing accounts.
/// </summary>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    /// <summary>OWASP Password Storage Cheat Sheet value for PBKDF2-HMAC-SHA512.</summary>
    public const int DefaultIterations = 210_000;

    private const string Scheme = "pbkdf2-sha512";
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    // Burned when no account matched, so an unknown email costs as much as a wrong password.
    private static readonly byte[] DummySalt = new byte[SaltSize];

    private readonly int _iterations;

    public Pbkdf2PasswordHasher()
        : this(DefaultIterations) { }

    /// <summary>Tests use a low count to stay fast; production uses <see cref="DefaultIterations"/>.</summary>
    internal Pbkdf2PasswordHasher(int iterations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);
        _iterations = iterations;
    }

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var digest = Derive(password, salt, _iterations);

        return string.Join(
            '.',
            Scheme,
            _iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(digest)
        );
    }

    public bool Verify(string password, string? passwordHash)
    {
        ArgumentNullException.ThrowIfNull(password);

        if (!TryParse(passwordHash, out var iterations, out var salt, out var expected))
        {
            _ = Derive(password, DummySalt, _iterations);
            return false;
        }

        var actual = Derive(password, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, Algorithm, HashSize);

    private static bool TryParse(string? value, out int iterations, out byte[] salt, out byte[] digest)
    {
        iterations = 0;
        salt = [];
        digest = [];

        var parts = value?.Split('.');
        if (parts is not { Length: 4 } || parts[0] != Scheme)
        {
            return false;
        }

        // Upper bound: a corrupted row must not make one login burn minutes of CPU.
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out iterations)
            || iterations is < 1 or > 10_000_000)
        {
            return false;
        }

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            digest = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        return salt.Length == SaltSize && digest.Length == HashSize;
    }
}
