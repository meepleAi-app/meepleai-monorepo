using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Command to save a cost estimation scenario.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal sealed record SaveCostScenarioCommand(
    string Name,
    string Strategy,
    string ModelId,
    int MessagesPerDay,
    int ActiveUsers,
    int AvgTokensPerRequest,
    decimal CostPerRequest,
    decimal DailyProjection,
    decimal MonthlyProjection,
    List<string>? Warnings,
    Guid CreatedByUserId) : ICommand<Guid>;

internal sealed class SaveCostScenarioCommandValidator : AbstractValidator<SaveCostScenarioCommand>
{
    public SaveCostScenarioCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Name is required and cannot exceed 200 characters");

        RuleFor(x => x.Strategy)
            .NotEmpty()
            .WithMessage("Strategy is required");

        RuleFor(x => x.ModelId)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("ModelId is required");

        RuleFor(x => x.MessagesPerDay)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MessagesPerDay cannot be negative");

        RuleFor(x => x.ActiveUsers)
            .GreaterThanOrEqualTo(0)
            .WithMessage("ActiveUsers cannot be negative");

        RuleFor(x => x.AvgTokensPerRequest)
            .GreaterThan(0)
            .WithMessage("AvgTokensPerRequest must be greater than zero");

        RuleFor(x => x.CostPerRequest)
            .GreaterThanOrEqualTo(0)
            .WithMessage("CostPerRequest cannot be negative");

        RuleFor(x => x.CreatedByUserId)
            .NotEmpty()
            .WithMessage("CreatedByUserId is required");
    }
}
