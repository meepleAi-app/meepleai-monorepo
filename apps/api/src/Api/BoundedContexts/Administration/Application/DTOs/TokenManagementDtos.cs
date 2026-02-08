namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// DTOs for Token Management API responses (Issue #3692)
/// Matches TypeScript schemas in apps/web/src/lib/api/schemas/admin.schemas.ts
/// </summary>

public sealed record TokenBalanceDto(
    decimal CurrentBalance,
    decimal TotalBudget,
    string Currency,
    double UsagePercent,
    int? ProjectedDaysUntilDepletion,
    DateTime LastUpdated);

public sealed record TokenConsumptionPointDto(
    string Date,
    int Tokens,
    decimal Cost);

public sealed record TokenConsumptionDataDto(
    List<TokenConsumptionPointDto> Points,
    int TotalTokens,
    decimal TotalCost,
    int AvgDailyTokens,
    decimal AvgDailyCost);

public sealed record TierUsageDto(
    string Tier,
    int LimitPerMonth,
    int CurrentUsage,
    int UserCount,
    double UsagePercent);

public sealed record TierUsageListDto(
    List<TierUsageDto> Tiers);

public sealed record TopConsumerDto(
    Guid UserId,
    string DisplayName,
    string Email,
    string Tier,
    int TokensUsed,
    double PercentOfTierLimit);

public sealed record TopConsumersListDto(
    List<TopConsumerDto> Consumers);

public sealed record AddCreditsRequest(
    decimal Amount,
    string Currency,
    string? Note);

public sealed record UpdateTierLimitsRequest(
    string Tier,
    int TokensPerMonth);
