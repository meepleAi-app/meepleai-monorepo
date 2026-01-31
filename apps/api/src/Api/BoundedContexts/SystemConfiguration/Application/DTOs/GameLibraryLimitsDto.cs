#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs

namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// Response DTO for game library tier limits configuration.
/// </summary>
internal record GameLibraryLimitsDto(
    int FreeTierLimit,
    int NormalTierLimit,
    int PremiumTierLimit,
    DateTime LastUpdatedAt,
    string? LastUpdatedByUserId
);

/// <summary>
/// Request DTO for updating game library tier limits.
/// </summary>
internal record UpdateGameLibraryLimitsRequest(
    int FreeTierLimit,
    int NormalTierLimit,
    int PremiumTierLimit
);
