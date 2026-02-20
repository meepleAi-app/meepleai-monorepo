using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for UpdateUserAgentCommand with tier-aware config validation.
/// Issue #4683: User Agent CRUD Endpoints + Tiered Config Validation.
///
/// Uses same IsTierLevel pattern as CreateUserAgentCommandValidator for consistency.
/// </summary>
internal sealed class UpdateUserAgentCommandValidator : AbstractValidator<UpdateUserAgentCommand>
{
    private static readonly HashSet<string> ValidStrategyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "SingleModel", "HybridSearch", "VectorOnly", "RetrievalOnly",
        "IterativeRAG", "SentenceWindowRAG", "ColBERTReranking",
        "ChainOfThoughtRAG", "QueryDecomposition", "MultiAgentRAG",
        "RAGFusion", "StepBackPrompting", "QueryExpansion"
    };

    public UpdateUserAgentCommandValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEmpty()
            .WithMessage("AgentId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Agent name cannot be empty or whitespace")
            .MaximumLength(100)
            .WithMessage("Agent name cannot exceed 100 characters")
            .When(x => x.Name != null);

        RuleFor(x => x.StrategyName)
            .Must(BeValidStrategyName!)
            .WithMessage("Invalid StrategyName. See documentation for valid strategies.")
            .When(x => x.StrategyName != null);

        // Free tier: Cannot change strategy
        RuleFor(x => x.StrategyName)
            .Null()
            .WithMessage("Free tier users cannot configure strategy. Upgrade to Normal tier or higher.")
            .When(x => IsTierLevel(x.UserTier, x.UserRole, 0));

        // Free+Normal tier: Cannot set parameters
        RuleFor(x => x.StrategyParameters)
            .Must(BeNullOrEmpty)
            .WithMessage("Strategy parameters require Premium tier or higher.")
            .When(x => IsTierLevel(x.UserTier, x.UserRole, 0) || IsTierLevel(x.UserTier, x.UserRole, 1));
    }

    private static bool BeValidStrategyName(string name)
        => ValidStrategyNames.Contains(name);

    private static bool BeNullOrEmpty(IDictionary<string, object>? parameters)
        => parameters == null || parameters.Count == 0;

    /// <summary>
    /// Consistent tier-level checking (same pattern as CreateUserAgentCommandValidator).
    /// Admin/Editor always returns false (never restricted).
    /// </summary>
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
