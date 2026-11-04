using Api.BoundedContexts.KnowledgeBase.Application.Commands.RagEnhancements;
using Api.Services;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators.RagEnhancements;

internal sealed class ToggleRagEnhancementCommandValidator : AbstractValidator<ToggleRagEnhancementCommand>
{
    public ToggleRagEnhancementCommandValidator()
    {
        // The route answers 400 for an unknown key before it sends the command; this rule keeps any other
        // sender from writing a flag that is not a RAG enhancement.
        RuleFor(x => x.FeatureKey)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("FeatureKey is required")
            .Must(key => FeatureFlagConstants.RagEnhancements.Contains(key, StringComparer.Ordinal))
            .WithMessage(x => $"Unknown enhancement key: {x.FeatureKey}");

        // Guid.Empty is what FeatureFlagService used when no id was passed, and what the configuration
        // validators then rejected (#4594).
        RuleFor(x => x.AdminId)
            .NotEqual(Guid.Empty)
            .WithMessage("AdminId is required");
    }
}
