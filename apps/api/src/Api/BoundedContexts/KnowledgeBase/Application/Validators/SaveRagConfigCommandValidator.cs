using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for SaveRagConfigCommand.
/// Issue #3304: RAG Dashboard configuration validation.
/// </summary>
internal sealed class SaveRagConfigCommandValidator : AbstractValidator<SaveRagConfigCommand>
{
    private static readonly string[] ValidStrategies =
    [
        "Hybrid", "Semantic", "Keyword", "Contextual", "MultiQuery", "Agentic"
    ];

    public SaveRagConfigCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Config)
            .NotNull()
            .WithMessage("Config is required");

        // Generation params validation
        RuleFor(x => x.Config.Generation.Temperature)
            .InclusiveBetween(0.0, 2.0)
            .WithMessage("Temperature must be between 0 and 2");

        RuleFor(x => x.Config.Generation.TopK)
            .GreaterThan(0)
            .WithMessage("TopK must be greater than 0");

        RuleFor(x => x.Config.Generation.TopP)
            .InclusiveBetween(0.0, 1.0)
            .WithMessage("TopP must be between 0 and 1");

        RuleFor(x => x.Config.Generation.MaxTokens)
            .InclusiveBetween(1, 128000)
            .WithMessage("MaxTokens must be between 1 and 128000");

        // Retrieval params validation
        RuleFor(x => x.Config.Retrieval.ChunkSize)
            .InclusiveBetween(100, 4000)
            .WithMessage("ChunkSize must be between 100 and 4000");

        RuleFor(x => x.Config.Retrieval.ChunkOverlap)
            .InclusiveBetween(0, 50)
            .WithMessage("ChunkOverlap must be between 0 and 50 percent");

        RuleFor(x => x.Config.Retrieval.TopResults)
            .InclusiveBetween(1, 50)
            .WithMessage("TopResults must be between 1 and 50");

        RuleFor(x => x.Config.Retrieval.SimilarityThreshold)
            .InclusiveBetween(0.0, 1.0)
            .WithMessage("SimilarityThreshold must be between 0 and 1");

        // Reranker settings validation
        RuleFor(x => x.Config.Reranker.Model)
            .NotEmpty()
            .When(x => x.Config.Reranker.Enabled)
            .WithMessage("Reranker model is required when reranker is enabled");

        RuleFor(x => x.Config.Reranker.TopN)
            .InclusiveBetween(1, 20)
            .When(x => x.Config.Reranker.Enabled)
            .WithMessage("Reranker TopN must be between 1 and 20");

        // Model selection validation
        RuleFor(x => x.Config.Models.PrimaryModel)
            .NotEmpty()
            .WithMessage("PrimaryModel is required");

        // Strategy-specific settings validation
        RuleFor(x => x.Config.StrategySpecific.HybridAlpha)
            .InclusiveBetween(0.0, 1.0)
            .WithMessage("HybridAlpha must be between 0 and 1");

        RuleFor(x => x.Config.StrategySpecific.ContextWindow)
            .InclusiveBetween(0, 20)
            .WithMessage("ContextWindow must be between 0 and 20");

        RuleFor(x => x.Config.StrategySpecific.MaxHops)
            .InclusiveBetween(1, 10)
            .WithMessage("MaxHops must be between 1 and 10");

        // Active strategy validation
        RuleFor(x => x.Config.ActiveStrategy)
            .NotEmpty()
            .WithMessage("ActiveStrategy is required")
            .Must(strategy => ValidStrategies.Contains(strategy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"ActiveStrategy must be one of: {string.Join(", ", ValidStrategies)}");
    }
}
