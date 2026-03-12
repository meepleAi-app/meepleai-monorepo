using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.EstimateAgentCost;

/// <summary>
/// Validator for EstimateAgentCostQuery.
/// Ensures GameId and DocumentIds are provided with reasonable limits.
/// </summary>
internal sealed class EstimateAgentCostQueryValidator : AbstractValidator<EstimateAgentCostQuery>
{
    private const int MaxDocumentIds = 10;

    public EstimateAgentCostQueryValidator()
    {
        RuleFor(x => x.GameId)
            .NotEqual(Guid.Empty).WithMessage("GameId is required");

        RuleFor(x => x.DocumentIds)
            .NotEmpty().WithMessage("DocumentIds is required")
            .Must(ids => ids.Count <= MaxDocumentIds)
            .WithMessage($"DocumentIds cannot exceed {MaxDocumentIds} items");

        RuleForEach(x => x.DocumentIds)
            .NotEqual(Guid.Empty).WithMessage("Each DocumentId must be a valid GUID");

        RuleFor(x => x.StrategyName)
            .NotEmpty().WithMessage("StrategyName is required");
    }
}
