namespace Api.BoundedContexts.BusinessSimulations.Application.DTOs;

/// <summary>
/// Application usage statistics response DTO.
/// Issue #4562: App Usage Stats API (Epic #3688)
/// </summary>
public sealed record AppUsageStatsDto(
    DauMauStatsDto DailyActiveUsers,
    DauMauStatsDto MonthlyActiveUsers,
    SessionAnalyticsDto Sessions,
    RetentionCohortDto Retention,
    IReadOnlyList<FeatureFunnelStepDto> FeatureFunnel,
    IReadOnlyList<GeoDistributionDto> GeoDistribution,
    DateTime GeneratedAt);

/// <summary>
/// DAU/MAU statistics with period comparison.
/// </summary>
public sealed record DauMauStatsDto(
    int Current,
    int Previous,
    double ChangePercentage);

/// <summary>
/// Session analytics with duration and engagement metrics.
/// </summary>
public sealed record SessionAnalyticsDto(
    string AverageDuration,
    double BounceRatePercentage,
    int TotalSessions,
    int ActiveSessions);

/// <summary>
/// User retention cohort analysis.
/// </summary>
public sealed record RetentionCohortDto(
    double Day7Percentage,
    double Day30Percentage,
    double Day90Percentage,
    int SignupsInPeriod);

/// <summary>
/// Feature adoption funnel step.
/// </summary>
public sealed record FeatureFunnelStepDto(
    string Step,
    int Users,
    double ConversionPercentage);

/// <summary>
/// Geographic user distribution.
/// </summary>
public sealed record GeoDistributionDto(
    string Country,
    string CountryCode,
    int Users,
    double Percentage);
