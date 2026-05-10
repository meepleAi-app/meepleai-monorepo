#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.Models;

internal sealed record ProviderProbeResultDto(
    string ProviderName,
    bool TokenConfigured,
    bool TokenAuthenticated,
    bool ModelAvailable,
    string? TokenFingerprint,
    string? ErrorCode,
    string? ErrorMessage,
    int LatencyMs,
    DateTime ProbedAt);
