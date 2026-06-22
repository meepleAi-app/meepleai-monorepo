using System.Security.Cryptography;
using System.Text;

namespace Api.Services.Providers.Probe;

/// <summary>
/// Computes SHA256(token) and returns first 8 lowercase hex chars.
/// Rationale: 32-bit space &gt;&gt; expected token count (&lt;50) → collision risk negligible.
/// 8 chars enough for audit "is it the same token?" without enabling brute-force.
/// </summary>
internal static class TokenFingerprint
{
    public static string? Compute(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var sb = new StringBuilder(8);
        for (int i = 0; i < 4; i++) sb.Append(bytes[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
