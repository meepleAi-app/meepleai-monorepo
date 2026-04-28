namespace Api.SharedKernel.Utilities;

/// <summary>
/// Minimal Base62 encoder for compact, URL-safe identifiers.
/// Used by domain value objects that need short tokens (e.g. <c>InvitationToken</c>).
/// </summary>
/// <remarks>
/// Encodes raw bytes as base62 (alphabet 0-9, A-Z, a-z). 16 bytes (~131 bits of entropy)
/// encode to 22 characters, providing collision-resistant invitation tokens that are
/// safe to embed in URL path segments without percent-encoding.
/// </remarks>
internal static class Base62
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int Base = 62;

    /// <summary>
    /// Encodes the given bytes as a base62 string.
    /// </summary>
    /// <remarks>
    /// Uses big-integer style positional division. Output length is deterministic for
    /// fixed-size inputs (16 bytes always produce 22 characters via left-padding).
    /// </remarks>
    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        // Convert big-endian byte sequence to a non-negative big integer represented as
        // an array of digits in base 62. We use a mutable byte array to support division.
        var workingBuffer = new byte[bytes.Length];
        bytes.CopyTo(workingBuffer);

        var digits = new List<char>(capacity: 22);

        // Repeatedly divide the big integer by 62, collecting remainders.
        while (!IsZero(workingBuffer))
        {
            int remainder = 0;
            for (int i = 0; i < workingBuffer.Length; i++)
            {
                int accumulator = (remainder << 8) | workingBuffer[i];
                workingBuffer[i] = (byte)(accumulator / Base);
                remainder = accumulator % Base;
            }
            digits.Add(Alphabet[remainder]);
        }

        // 16 bytes → up to 22 base62 chars. Pad with leading '0' so the encoding is
        // length-deterministic (matches InvitationToken contract: always 22 chars).
        // 22 = ceil(16 * log(256) / log(62)) = ceil(21.49) = 22
        const int FixedLength = 22;
        while (bytes.Length == 16 && digits.Count < FixedLength)
        {
            digits.Add(Alphabet[0]);
        }

        digits.Reverse();
        return new string(digits.ToArray());
    }

    private static bool IsZero(ReadOnlySpan<byte> bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] != 0)
            {
                return false;
            }
        }
        return true;
    }
}
