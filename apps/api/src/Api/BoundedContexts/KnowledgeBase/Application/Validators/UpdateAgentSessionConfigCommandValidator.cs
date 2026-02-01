using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for UpdateAgentSessionConfigCommand.
/// Issue #3253 (BACK-AGT-002): PATCH Endpoint - Update Agent Runtime Config.
/// </summary>
internal sealed class UpdateAgentSessionConfigCommandValidator : AbstractValidator<UpdateAgentSessionConfigCommand>
{
    private static readonly string[] AllowedRagStrategies = { "HybridSearch", "VectorOnly", "MultiModelConsensus" };

    public UpdateAgentSessionConfigCommandValidator()
    {
        RuleFor(x => x.AgentSessionId)
            .NotEqual(Guid.Empty).WithMessage("AgentSessionId is required");

        RuleFor(x => x.ModelType)
            .NotEmpty().WithMessage("ModelType is required")
            .MaximumLength(50).WithMessage("ModelType cannot exceed 50 characters");

        RuleFor(x => x.Temperature)
            .InclusiveBetween(0, 2).WithMessage("Temperature must be between 0 and 2");

        RuleFor(x => x.MaxTokens)
            .InclusiveBetween(512, 8192).WithMessage("MaxTokens must be between 512 and 8192");

        RuleFor(x => x.RagStrategy)
            .NotEmpty().WithMessage("RagStrategy is required")
            .Must(s => AllowedRagStrategies.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"RagStrategy must be one of: {string.Join(", ", AllowedRagStrategies)}");

        RuleFor(x => x.RagParams)
            .NotNull().WithMessage("RagParams cannot be null");
    }
}
