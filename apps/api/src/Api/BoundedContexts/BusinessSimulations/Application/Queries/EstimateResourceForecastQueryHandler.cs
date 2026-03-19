using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Computes 12-month resource projections based on current metrics and growth parameters.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
internal sealed class EstimateResourceForecastQueryHandler
    : IQueryHandler<EstimateResourceForecastQuery, ResourceForecastEstimationResult>
{
    private const int ProjectionMonths = 12;
    private const int DaysPerMonth = 30;

    // Resource cost estimates (USD/month)
    private const decimal DbCostPerGbMonth = 0.10m;       // PostgreSQL managed ~$0.10/GB/month
    private const decimal TokenCostPer1MTokens = 0.50m;   // Average LLM cost per 1M tokens
    private const decimal CacheCostPerGbMonth = 0.50m;    // Redis managed ~$0.50/GB/month
    private const decimal VectorCostPer1MEntries = 5.00m; // Qdrant managed ~$5/1M entries/month

    // Threshold limits for recommendations
    private const decimal DbWarningGb = 50m;
    private const decimal DbCriticalGb = 100m;
    private const long TokenWarningPerDay = 10_000_000;
    private const long TokenCriticalPerDay = 50_000_000;
    private const decimal CacheWarningMb = 2_048m;    // 2 GB
    private const decimal CacheCriticalMb = 8_192m;   // 8 GB
    private const long VectorWarningCount = 5_000_000;
    private const long VectorCriticalCount = 20_000_000;

    private readonly ILogger<EstimateResourceForecastQueryHandler> _logger;

    public EstimateResourceForecastQueryHandler(ILogger<EstimateResourceForecastQueryHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<ResourceForecastEstimationResult> Handle(
        EstimateResourceForecastQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var projections = new List<MonthlyProjection>(ProjectionMonths);
        var recommendations = new List<ForecastRecommendation>();

        var currentUsers = query.CurrentUsers;
        var growthRate = query.MonthlyGrowthRate / 100m;

        for (var month = 1; month <= ProjectionMonths; month++)
        {
            var projectedUsers = CalculateProjectedUsers(
                currentUsers, growthRate, month, query.GrowthPattern);

            var newUsers = projectedUsers - query.CurrentUsers;
            var additionalDbMb = newUsers * query.DbPerUserMb;
            var projectedDbGb = query.CurrentDbSizeGb + (additionalDbMb / 1024m);

            var projectedDailyTokens = query.CurrentDailyTokens +
                (newUsers * query.TokensPerUserPerDay);

            var additionalCacheMb = newUsers * query.CachePerUserMb;
            var projectedCacheMb = query.CurrentCacheMb + additionalCacheMb;

            var projectedVectors = query.CurrentVectorEntries +
                (newUsers * query.VectorsPerUser);

            var monthlyCost = CalculateMonthlyCost(
                projectedDbGb, projectedDailyTokens, projectedCacheMb, projectedVectors);

            projections.Add(new MonthlyProjection(
                Month: month,
                ProjectedUsers: projectedUsers,
                ProjectedDbGb: Math.Round(projectedDbGb, 2),
                ProjectedDailyTokens: projectedDailyTokens,
                ProjectedCacheMb: Math.Round(projectedCacheMb, 2),
                ProjectedVectorEntries: projectedVectors,
                EstimatedMonthlyCostUsd: Math.Round(monthlyCost, 2)));

            // Check thresholds and generate recommendations
            CheckDbThreshold(projectedDbGb, month, recommendations);
            CheckTokenThreshold(projectedDailyTokens, month, recommendations);
            CheckCacheThreshold(projectedCacheMb, month, recommendations);
            CheckVectorThreshold(projectedVectors, month, recommendations);
        }

        // Deduplicate recommendations: keep only the first occurrence per resource+severity
        var deduped = recommendations
            .GroupBy(r => $"{r.ResourceType}:{r.Severity}", StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(r => r.TriggerMonth)
            .ToList();

        var lastProjection = projections[^1];

        var result = new ResourceForecastEstimationResult(
            GrowthPattern: query.GrowthPattern,
            MonthlyGrowthRate: query.MonthlyGrowthRate,
            CurrentUsers: query.CurrentUsers,
            ProjectedUsersMonth12: lastProjection.ProjectedUsers,
            Projections: projections,
            Recommendations: deduped,
            ProjectedMonthlyCostMonth12: lastProjection.EstimatedMonthlyCostUsd);

        _logger.LogInformation(
            "Resource forecast computed: {Pattern} {Rate}%/mo, {CurrentUsers} → {ProjectedUsers} users, ${Cost}/mo at month 12, {RecCount} recommendations",
            query.GrowthPattern, query.MonthlyGrowthRate, query.CurrentUsers,
            lastProjection.ProjectedUsers, lastProjection.EstimatedMonthlyCostUsd, deduped.Count);

        return Task.FromResult(result);
    }

    private static long CalculateProjectedUsers(int currentUsers, decimal growthRate, int month, string pattern)
    {
        if (currentUsers == 0) return 0;
        if (growthRate == 0) return currentUsers;

        var projected = pattern.ToUpperInvariant() switch
        {
            "LINEAR" => currentUsers * (1m + (growthRate * month)),
            "EXPONENTIAL" => currentUsers * (decimal)Math.Pow((double)(1m + growthRate), month),
            "LOGARITHMIC" => currentUsers * (1m + (growthRate * (decimal)Math.Log(1 + month))),
            "SCURVE" => CalculateSCurve(currentUsers, growthRate, month),
            _ => currentUsers * (1m + (growthRate * month)) // fallback to linear
        };

        return Math.Max(currentUsers, (long)Math.Ceiling(projected));
    }

    private static decimal CalculateSCurve(int currentUsers, decimal growthRate, int month)
    {
        // S-curve: logistic function with midpoint at month 6
        // Growth is slow at start, accelerates in middle, then plateaus
        var maxGrowthMultiplier = 1m + (growthRate * 12m); // Max growth at month 12
        var midpoint = 6.0;
        var steepness = 0.8;

        var logisticValue = 1.0 / (1.0 + Math.Exp(-steepness * ((double)month - midpoint)));
        var normalizedValue = (decimal)logisticValue;

        return currentUsers * (1m + ((maxGrowthMultiplier - 1m) * normalizedValue));
    }

    private static decimal CalculateMonthlyCost(
        decimal dbGb, long dailyTokens, decimal cacheMb, long vectorEntries)
    {
        var dbCost = dbGb * DbCostPerGbMonth;
        var tokenCost = ((decimal)dailyTokens * DaysPerMonth / 1_000_000m) * TokenCostPer1MTokens;
        var cacheCost = (cacheMb / 1024m) * CacheCostPerGbMonth;
        var vectorCost = ((decimal)vectorEntries / 1_000_000m) * VectorCostPer1MEntries;

        return dbCost + tokenCost + cacheCost + vectorCost;
    }

    private static void CheckDbThreshold(decimal dbGb, int month, List<ForecastRecommendation> recs)
    {
        if (dbGb >= DbCriticalGb)
        {
            recs.Add(new ForecastRecommendation(
                ResourceType: "Database",
                TriggerMonth: month,
                Severity: "critical",
                Message: $"Database projected to reach {dbGb:F1} GB by month {month}",
                Action: "Upgrade to larger DB instance or implement data archival strategy"));
        }
        else if (dbGb >= DbWarningGb)
        {
            recs.Add(new ForecastRecommendation(
                ResourceType: "Database",
                TriggerMonth: month,
                Severity: "warning",
                Message: $"Database projected to reach {dbGb:F1} GB by month {month}",
                Action: "Plan database scaling or implement cleanup policies"));
        }
    }

    private static void CheckTokenThreshold(long dailyTokens, int month, List<ForecastRecommendation> recs)
    {
        if (dailyTokens >= TokenCriticalPerDay)
        {
            recs.Add(new ForecastRecommendation(
                ResourceType: "TokenUsage",
                TriggerMonth: month,
                Severity: "critical",
                Message: $"Daily token usage projected to reach {dailyTokens:N0} by month {month}",
                Action: "Upgrade token tier or implement rate limiting and caching"));
        }
        else if (dailyTokens >= TokenWarningPerDay)
        {
            recs.Add(new ForecastRecommendation(
                ResourceType: "TokenUsage",
                TriggerMonth: month,
                Severity: "warning",
                Message: $"Daily token usage projected to reach {dailyTokens:N0} by month {month}",
                Action: "Review token usage patterns and consider budget increase"));
        }
    }

    private static void CheckCacheThreshold(decimal cacheMb, int month, List<ForecastRecommendation> recs)
    {
        if (cacheMb >= CacheCriticalMb)
        {
            recs.Add(new ForecastRecommendation(
                ResourceType: "CacheMemory",
                TriggerMonth: month,
                Severity: "critical",
                Message: $"Cache projected to reach {cacheMb:F0} MB by month {month}",
                Action: "Scale Redis cluster or implement eviction policies"));
        }
        else if (cacheMb >= CacheWarningMb)
        {
            recs.Add(new ForecastRecommendation(
                ResourceType: "CacheMemory",
                TriggerMonth: month,
                Severity: "warning",
                Message: $"Cache projected to reach {cacheMb:F0} MB by month {month}",
                Action: "Plan cache memory scaling or optimize TTL settings"));
        }
    }

    private static void CheckVectorThreshold(long vectorEntries, int month, List<ForecastRecommendation> recs)
    {
        if (vectorEntries >= VectorCriticalCount)
        {
            recs.Add(new ForecastRecommendation(
                ResourceType: "VectorStorage",
                TriggerMonth: month,
                Severity: "critical",
                Message: $"Vector entries projected to reach {vectorEntries:N0} by month {month}",
                Action: "Scale Qdrant cluster or implement vector pruning strategy"));
        }
        else if (vectorEntries >= VectorWarningCount)
        {
            recs.Add(new ForecastRecommendation(
                ResourceType: "VectorStorage",
                TriggerMonth: month,
                Severity: "warning",
                Message: $"Vector entries projected to reach {vectorEntries:N0} by month {month}",
                Action: "Plan vector DB scaling or review embedding retention policy"));
        }
    }
}
