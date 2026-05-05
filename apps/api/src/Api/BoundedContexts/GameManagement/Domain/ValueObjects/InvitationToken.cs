using System.Security.Cryptography;
using Api.SharedKernel.Utilities;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing a public invitation token used for game-night RSVP.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
/// <remarks>
/// <para>
/// Token shape: 22 base62 characters (alphabet 0-9, A-Z, a-z) encoding 16 bytes
/// of cryptographic randomness (~131 bits of entropy). Suitable for URL path segments
/// without percent-encoding and resistant to brute-force enumeration.
/// </para>
/// <para>
/// Generated tokens MUST come from <see cref="Generate"/> (uses
/// <see cref="RandomNumberGenerator"/>). The <see cref="Create"/> factory exists for
/// reconstitution from persisted state and validates length + alphabet.
/// </para>
/// </remarks>
public sealed record InvitationToken
{
    /// <summary>
    /// Fixed length of every generated invitation token (22 base62 characters).
    /// </summary>
    public const int Length = 22;

    /// <summary>
    /// The token string. Always 22 characters, base62 alphabet.
    /// </summary>
    public string Value { get; }

    private InvitationToken(string value) => Value = value;

    /// <summary>
    /// Reconstitutes a token from a persisted string. Validates length and alphabet.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the input is null/empty/wrong length, or contains non-base62 characters.
    /// </exception>
    public static InvitationToken Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Token cannot be empty", nameof(value));
        }
        if (value.Length != Length)
        {
            throw new ArgumentException($"Token must be {Length} characters", nameof(value));
        }
        if (!IsValidBase62(value))
        {
            throw new ArgumentException("Token must contain only base62 characters", nameof(value));
        }

        return new InvitationToken(value);
    }

    /// <summary>
    /// Generates a fresh, cryptographically random invitation token.
    /// </summary>
    public static InvitationToken Generate()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return new InvitationToken(Base62.Encode(bytes));
    }

    /// <summary>
    /// Implicit conversion to string for ergonomic use as cache keys / URL segments.
    /// </summary>
    public static implicit operator string(InvitationToken token) => token.Value;

    public override string ToString() => Value;

    private static bool IsValidBase62(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            bool isValid = (c >= '0' && c <= '9')
                           || (c >= 'A' && c <= 'Z')
                           || (c >= 'a' && c <= 'z');
            if (!isValid)
            {
                return false;
            }
        }
        return true;
    }
}
