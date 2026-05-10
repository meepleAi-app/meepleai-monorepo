#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.Models;

/// <summary>
/// Result of a provider token probe. Issue #936 (G1).
/// Outcome reflects authentication state only — provider reachable + token accepted.
/// ModelAvailable is informational and only populated when caller passes ?model=X.
/// </summary>
internal sealed record ProviderProbeResultDto(
    string ProviderName,
    bool TokenConfigured,
    bool TokenAuthenticated,
    bool? ModelAvailable,        // null = not checked (no expectedModel), true/false = check executed
    string? ExpectedModel,        // echoed back so caller knows what was checked
    string? TokenFingerprint,
    string? ErrorCode,
    string? ErrorMessage,
    int LatencyMs,
    DateTime ProbedAt);
