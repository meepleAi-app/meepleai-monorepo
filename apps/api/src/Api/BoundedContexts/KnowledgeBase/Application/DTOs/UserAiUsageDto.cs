namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Detailed AI usage statistics for a user.
/// Issue #3338: AI Token Usage Tracking per User
/// </summary>
/// <param name="UserId">User identifier</param>
/// <param name="Period">Date range for the usage data</param>
/// <param name="TotalTokens">Total tokens consumed</param>
/// <param name="TotalCostUsd">Total cost in USD</param>
/// <param name="RequestCount">Number of AI requests</param>
/// <param name="ByModel">Token usage breakdown by model</param>
/// <param name="ByOperation">Usage breakdown by operation type</param>
/// <param name="DailyUsage">Daily token usage time series</param>
public record UserAiUsageDto(
    Guid UserId,
    UsagePeriodDto Period,
    long TotalTokens,
    decimal TotalCostUsd,
    int RequestCount,
    IReadOnlyList<ModelUsageDto> ByModel,
    IReadOnlyList<OperationUsageDto> ByOperation,
    IReadOnlyList<DailyUsageDto> DailyUsage
);

/// <summary>
/// Date range period for usage data.
/// </summary>
/// <param name="From">Start date (inclusive)</param>
/// <param name="To">End date (inclusive)</param>
public record UsagePeriodDto(
    DateOnly From,
    DateOnly To
);

/// <summary>
/// Token usage breakdown by model.
/// </summary>
/// <param name="Model">Model identifier (e.g., "meta-llama/llama-3.3-70b-instruct:free")</param>
/// <param name="Tokens">Total tokens for this model</param>
/// <param name="Cost">Total cost in USD for this model</param>
public record ModelUsageDto(
    string Model,
    long Tokens,
    decimal Cost
);

/// <summary>
/// Usage breakdown by operation type.
/// </summary>
/// <param name="Operation">Operation type (e.g., "chat", "rag_query", "embedding")</param>
/// <param name="Count">Number of requests</param>
/// <param name="Tokens">Total tokens for this operation</param>
public record OperationUsageDto(
    string Operation,
    int Count,
    long Tokens
);

/// <summary>
/// Daily token usage data point.
/// </summary>
/// <param name="Date">Date (YYYY-MM-DD format)</param>
/// <param name="Tokens">Total tokens for this day</param>
public record DailyUsageDto(
    DateOnly Date,
    long Tokens
);
