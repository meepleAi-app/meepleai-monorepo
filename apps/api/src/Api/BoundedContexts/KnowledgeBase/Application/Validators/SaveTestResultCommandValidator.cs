using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for SaveTestResultCommand.
/// </summary>
internal sealed class SaveTestResultCommandValidator : AbstractValidator<SaveTestResultCommand>
{
    public SaveTestResultCommandValidator()
    {
        RuleFor(x => x.TypologyId)
            .NotEmpty()
            .WithMessage("TypologyId is required");

        RuleFor(x => x.Query)
            .NotEmpty()
            .WithMessage("Query is required")
            .MaximumLength(2000)
            .WithMessage("Query cannot exceed 2000 characters");

        RuleFor(x => x.Response)
            .NotEmpty()
            .WithMessage("Response is required");

        RuleFor(x => x.ModelUsed)
            .NotEmpty()
            .WithMessage("ModelUsed is required")
            .MaximumLength(200)
            .WithMessage("ModelUsed cannot exceed 200 characters");

        RuleFor(x => x.ConfidenceScore)
            .InclusiveBetween(0.0, 1.0)
            .WithMessage("ConfidenceScore must be between 0.0 and 1.0");

        RuleFor(x => x.TokensUsed)
            .GreaterThanOrEqualTo(0)
            .WithMessage("TokensUsed cannot be negative");

        RuleFor(x => x.CostEstimate)
            .GreaterThanOrEqualTo(0)
            .WithMessage("CostEstimate cannot be negative");

        RuleFor(x => x.LatencyMs)
            .GreaterThanOrEqualTo(0)
            .WithMessage("LatencyMs cannot be negative");

        RuleFor(x => x.ExecutedBy)
            .NotEmpty()
            .WithMessage("ExecutedBy is required");

        RuleFor(x => x.StrategyOverride)
            .MaximumLength(200)
            .When(x => x.StrategyOverride != null)
            .WithMessage("StrategyOverride cannot exceed 200 characters");
    }
}
