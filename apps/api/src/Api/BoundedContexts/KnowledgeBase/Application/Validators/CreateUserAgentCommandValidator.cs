using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for CreateUserAgentCommand with tier-aware config validation.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
///
/// Tier rules:
/// - Free: Only AgentType allowed (strategy defaults to SingleModel)
/// - Normal: AgentType + StrategyName allowed (parameters still default)
/// - Premium/Admin: Full config (topK, minScore, temperature, etc.)
/// </summary>
internal sealed class CreateUserAgentCommandValidator : AbstractValidator<CreateUserAgentCommand>
{
    private static readonly HashSet<string> ValidAgentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "RAG", "Citation", "Confidence", "RulesInterpreter", "Conversation"
    };

    private static readonly HashSet<string> ValidStrategyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "SingleModel", "HybridSearch", "VectorOnly", "RetrievalOnly",
        "IterativeRAG", "SentenceWindowRAG", "ColBERTReranking",
        "ChainOfThoughtRAG", "QueryDecomposition", "MultiAgentRAG",
        "RAGFusion", "StepBackPrompting", "QueryExpansion"
    };

    public CreateUserAgentCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.AgentType)
            .NotEmpty()
            .WithMessage("AgentType is required")
            .Must(BeValidAgentType)
            .WithMessage("Invalid AgentType. Valid types: RAG, Citation, Confidence, RulesInterpreter, Conversation");

        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Agent name cannot be empty or whitespace")
            .MaximumLength(100)
            .WithMessage("Agent name cannot exceed 100 characters")
            .When(x => x.Name != null);

        // Free tier: StrategyName must be null
        RuleFor(x => x.StrategyName)
            .Null()
            .WithMessage("Free tier users cannot configure strategy. Upgrade to Normal tier or higher.")
            .When(x => IsTierLevel(x.UserTier, x.UserRole, 0));

        // Normal tier: StrategyName allowed, but parameters must be null/empty
        RuleFor(x => x.StrategyName)
            .Must(BeValidStrategyName!)
            .WithMessage("Invalid StrategyName. See documentation for valid strategies.")
            .When(x => x.StrategyName != null && !IsTierLevel(x.UserTier, x.UserRole, 0));

        // Free+Normal tier: StrategyParameters must be null/empty
        RuleFor(x => x.StrategyParameters)
            .Must(BeNullOrEmpty)
            .WithMessage("Strategy parameters require Premium tier or higher.")
            .When(x => IsTierLevel(x.UserTier, x.UserRole, 0) || IsTierLevel(x.UserTier, x.UserRole, 1));
    }

    private static bool BeValidAgentType(string type)
        => ValidAgentTypes.Contains(type);

    private static bool BeValidStrategyName(string name)
        => ValidStrategyNames.Contains(name);

    private static bool BeNullOrEmpty(IDictionary<string, object>? parameters)
        => parameters == null || parameters.Count == 0;

    private static bool IsTierLevel(string tier, string role, int level)
    {
        // Admin/Editor bypass - treat as premium
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Editor", StringComparison.OrdinalIgnoreCase))
            return false; // Never restricted

        return tier?.ToLowerInvariant() switch
        {
            "free" => level == 0,
            "normal" => level == 1,
            "premium" or "pro" => level == 2,
            "enterprise" => level == 3,
            _ => level == 0 // Default to free
        };
    }
}
