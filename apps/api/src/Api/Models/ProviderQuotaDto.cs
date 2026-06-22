#pragma warning disable MA0048
namespace Api.Models;

/// <summary>
/// Provider quota / balance information. Issue #936 (G2).
/// </summary>
internal sealed record ProviderQuotaDto(
    string ProviderName,
    bool QuotaSupported,        // false → all numeric fields null
    bool TokenConfigured,
    decimal? UsedUsd,
    decimal? LimitUsd,
    decimal? RemainingUsd,
    DateTime? ResetAt,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime FetchedAt,
    int CacheTtlSeconds);
