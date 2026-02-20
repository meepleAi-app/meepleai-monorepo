#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs

namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// Response DTO for chat history tier limits configuration.
/// </summary>
internal record ChatHistoryLimitsDto(
    int FreeTierLimit,
    int NormalTierLimit,
    int PremiumTierLimit,
    DateTime LastUpdatedAt,
    string? LastUpdatedByUserId
);

/// <summary>
/// Request DTO for updating chat history tier limits.
/// </summary>
internal record UpdateChatHistoryLimitsRequest(
    int FreeTierLimit,
    int NormalTierLimit,
    int PremiumTierLimit
);
