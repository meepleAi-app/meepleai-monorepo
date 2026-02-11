using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Command to save a resource forecast scenario.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
internal sealed record SaveResourceForecastCommand(
    string Name,
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
    int VectorsPerUser,
    string ProjectionsJson,
    string? RecommendationsJson,
    decimal ProjectedMonthlyCost,
    Guid CreatedByUserId) : ICommand<Guid>;

internal sealed class SaveResourceForecastCommandValidator : AbstractValidator<SaveResourceForecastCommand>
{
    public SaveResourceForecastCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Name is required and cannot exceed 200 characters");

        RuleFor(x => x.GrowthPattern)
            .NotEmpty()
            .WithMessage("GrowthPattern is required");

        RuleFor(x => x.MonthlyGrowthRate)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(100)
            .WithMessage("MonthlyGrowthRate must be between 0 and 100");

        RuleFor(x => x.CurrentUsers)
            .GreaterThanOrEqualTo(0)
            .WithMessage("CurrentUsers cannot be negative");

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

        RuleFor(x => x.ProjectionsJson)
            .NotEmpty()
            .WithMessage("ProjectionsJson is required");

        RuleFor(x => x.ProjectedMonthlyCost)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ProjectedMonthlyCost cannot be negative");

        RuleFor(x => x.CreatedByUserId)
            .NotEmpty()
            .WithMessage("CreatedByUserId is required");
    }
}
