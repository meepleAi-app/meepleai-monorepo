using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Query to compute 12-month resource projections based on current metrics and growth parameters.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
internal sealed record EstimateResourceForecastQuery(
    string GrowthPattern,
    decimal MonthlyGrowthRate,
    int CurrentUsers,
    decimal CurrentDbSizeGb,
    long CurrentDailyTokens,
    decimal CurrentCacheMb,
    long CurrentVectorEntries,
    decimal DbPerUserMb,
    int TokensPerUserPerDay,
    decimal CachePerUserMb,
    int VectorsPerUser) : IQuery<ResourceForecastEstimationResult>;

/// <summary>
/// Single month projection data point.
/// </summary>
internal sealed record MonthlyProjection(
    int Month,
    long ProjectedUsers,
    decimal ProjectedDbGb,
    long ProjectedDailyTokens,
    decimal ProjectedCacheMb,
    long ProjectedVectorEntries,
    decimal EstimatedMonthlyCostUsd);

/// <summary>
/// Action recommendation based on resource thresholds.
/// </summary>
internal sealed record ForecastRecommendation(
    string ResourceType,
    int TriggerMonth,
    string Severity,
    string Message,
    string Action);

/// <summary>
/// Result of a resource forecast estimation.
/// </summary>
internal sealed record ResourceForecastEstimationResult(
    string GrowthPattern,
    decimal MonthlyGrowthRate,
    int CurrentUsers,
    long ProjectedUsersMonth12,
    List<MonthlyProjection> Projections,
    List<ForecastRecommendation> Recommendations,
    decimal ProjectedMonthlyCostMonth12);

internal sealed class EstimateResourceForecastQueryValidator : AbstractValidator<EstimateResourceForecastQuery>
{
    private static readonly string[] ValidPatterns = ["Linear", "Exponential", "Logarithmic", "SCurve"];

    public EstimateResourceForecastQueryValidator()
    {
        RuleFor(x => x.GrowthPattern)
            .NotEmpty()
            .Must(p => ValidPatterns.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage("GrowthPattern must be Linear, Exponential, Logarithmic, or SCurve");

        RuleFor(x => x.MonthlyGrowthRate)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(100)
            .WithMessage("MonthlyGrowthRate must be between 0 and 100");

        RuleFor(x => x.CurrentUsers)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(100_000_000)
            .WithMessage("CurrentUsers must be between 0 and 100,000,000");

        RuleFor(x => x.CurrentDbSizeGb)
            .GreaterThanOrEqualTo(0)
            .WithMessage("CurrentDbSizeGb cannot be negative");

        RuleFor(x => x.CurrentDailyTokens)
            .GreaterThanOrEqualTo(0)
            .WithMessage("CurrentDailyTokens cannot be negative");

        RuleFor(x => x.CurrentCacheMb)
            .GreaterThanOrEqualTo(0)
            .WithMessage("CurrentCacheMb cannot be negative");

        RuleFor(x => x.CurrentVectorEntries)
            .GreaterThanOrEqualTo(0)
            .WithMessage("CurrentVectorEntries cannot be negative");

        RuleFor(x => x.DbPerUserMb)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(10_000)
            .WithMessage("DbPerUserMb must be between 0 and 10,000");

        RuleFor(x => x.TokensPerUserPerDay)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(10_000_000)
            .WithMessage("TokensPerUserPerDay must be between 0 and 10,000,000");

        RuleFor(x => x.CachePerUserMb)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(10_000)
            .WithMessage("CachePerUserMb must be between 0 and 10,000");

        RuleFor(x => x.VectorsPerUser)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1_000_000)
            .WithMessage("VectorsPerUser must be between 0 and 1,000,000");
    }
}
