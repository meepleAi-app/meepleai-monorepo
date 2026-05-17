using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;

namespace Api.BoundedContexts.SystemConfiguration.Application.Services;

/// <summary>
/// Issue #1089: Computes a deterministic GUID identifier for a status banner message
/// from its (Message, Severity, UpdatedAt) tuple. Used by the frontend to track
/// per-message dismissals across page reloads.
/// </summary>
internal static class StatusBannerMessageId
{
    /// <summary>
    /// Deterministic 16-byte GUID derived from the first 16 bytes of the SHA-256 hash of the
    /// canonical input. Used as a stable identifier for frontend dismissal tracking — not
    /// for security or integrity purposes.
    /// </summary>
    public static Guid Compute(string message, BannerSeverity severity, DateTime updatedAt)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{message ?? string.Empty}|{(int)severity}|{updatedAt.ToUniversalTime():O}");

        var bytes = Encoding.UTF8.GetBytes(canonical);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return new Guid(hash[..16]);
    }
}
