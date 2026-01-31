using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for UpdateAgentSessionStateCommand.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal sealed class UpdateAgentSessionStateCommandValidator : AbstractValidator<UpdateAgentSessionStateCommand>
{
    private const int MaxGameStateJsonLength = 50000;

    public UpdateAgentSessionStateCommandValidator()
    {
        RuleFor(x => x.AgentSessionId)
            .NotEqual(Guid.Empty).WithMessage("AgentSessionId is required");

        RuleFor(x => x.GameStateJson)
            .NotEmpty().WithMessage("GameStateJson is required")
            .MaximumLength(MaxGameStateJsonLength)
            .WithMessage($"GameStateJson cannot exceed {MaxGameStateJsonLength} characters");
    }
}
