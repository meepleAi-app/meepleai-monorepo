using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ResetRagConfigCommand.
/// Issue #3304: RAG Dashboard configuration validation.
/// </summary>
internal sealed class ResetRagConfigCommandValidator : AbstractValidator<ResetRagConfigCommand>
{
    private static readonly string[] ValidStrategies =
    [
        "Hybrid", "Semantic", "Keyword", "Contextual", "MultiQuery", "Agentic"
    ];

    public ResetRagConfigCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Strategy)
            .Must(strategy => strategy == null || ValidStrategies.Contains(strategy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Strategy must be null (reset all) or one of: {string.Join(", ", ValidStrategies)}");
    }
}
