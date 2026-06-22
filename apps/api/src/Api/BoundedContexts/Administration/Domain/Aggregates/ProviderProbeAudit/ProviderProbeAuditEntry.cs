using System.Text.RegularExpressions;

namespace Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;

/// <summary>
/// Audit entry for a provider token probe attempt. ISSUE-936 (G3).
/// Immutable after creation. Token never stored — only 8-char SHA256 fingerprint.
/// </summary>
internal sealed class ProviderProbeAuditEntry
{
#pragma warning disable MA0009 // Safe: bounded pattern, 8 hex chars only
    private static readonly Regex FingerprintRegex = new("^[a-f0-9]{8}$", RegexOptions.Compiled);
#pragma warning restore MA0009

    public Guid Id { get; private set; }
    public string ProviderName { get; private set; } = null!;
    public Guid ActorId { get; private set; }
    public string? TokenFingerprint { get; private set; }
    public ProbeOutcome Outcome { get; private set; }
    public string? ErrorCode { get; private set; }
    public int LatencyMs { get; private set; }
    public DateTime ProbedAt { get; private set; }

    private ProviderProbeAuditEntry() { } // EF

    public static ProviderProbeAuditEntry Create(
        string providerName,
        Guid actorId,
        string? tokenFingerprint,
        ProbeOutcome outcome,
        string? errorCode,
        int latencyMs)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name required", nameof(providerName));
        if (latencyMs < 0)
            throw new ArgumentOutOfRangeException(nameof(latencyMs), "Latency must be non-negative");
        if (tokenFingerprint is not null && !FingerprintRegex.IsMatch(tokenFingerprint))
            throw new ArgumentException("Fingerprint must be 8 lowercase hex chars", nameof(tokenFingerprint));

        return new ProviderProbeAuditEntry
        {
            Id = Guid.NewGuid(),
            ProviderName = providerName,
            ActorId = actorId,
            TokenFingerprint = tokenFingerprint,
            Outcome = outcome,
            ErrorCode = errorCode,
            LatencyMs = latencyMs,
            ProbedAt = DateTime.UtcNow
        };
    }
}
