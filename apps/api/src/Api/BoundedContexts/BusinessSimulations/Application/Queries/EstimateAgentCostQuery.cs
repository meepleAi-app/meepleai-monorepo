using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Query to estimate agent costs based on strategy, model, and usage parameters.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal sealed record EstimateAgentCostQuery(
    string Strategy,
    string ModelId,
    int MessagesPerDay,
    int ActiveUsers,
    int AvgTokensPerRequest) : IQuery<AgentCostEstimationResult>;

/// <summary>
/// Result of an agent cost estimation.
/// </summary>
internal sealed record AgentCostEstimationResult(
    string Strategy,
    string ModelId,
    string Provider,
    decimal InputCostPer1MTokens,
    decimal OutputCostPer1MTokens,
    decimal CostPerRequest,
    decimal DailyProjection,
    decimal MonthlyProjection,
    long TotalDailyRequests,
    int AvgTokensPerRequest,
    List<string> Warnings);

internal sealed class EstimateAgentCostQueryValidator : AbstractValidator<EstimateAgentCostQuery>
{
    private static readonly string[] ValidStrategies =
    [
        "Fast", "Balanced", "Precise", "Expert", "Consensus",
        "SentenceWindow", "Iterative", "Custom", "MultiAgent",
        "StepBack", "QueryExpansion", "RagFusion"
    ];

    public EstimateAgentCostQueryValidator()
    {
        RuleFor(x => x.Strategy)
            .NotEmpty()
            .Must(s => ValidStrategies.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Strategy must be a valid RAG strategy");

        RuleFor(x => x.ModelId)
            .NotEmpty()
            .MaximumLength(100)
            .WithMessage("ModelId is required");

        RuleFor(x => x.MessagesPerDay)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1_000_000)
            .WithMessage("MessagesPerDay must be between 0 and 1,000,000");

        RuleFor(x => x.ActiveUsers)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(10_000_000)
            .WithMessage("ActiveUsers must be between 0 and 10,000,000");

        RuleFor(x => x.AvgTokensPerRequest)
            .GreaterThan(0)
            .LessThanOrEqualTo(100_000)
            .WithMessage("AvgTokensPerRequest must be between 1 and 100,000");
    }
}
