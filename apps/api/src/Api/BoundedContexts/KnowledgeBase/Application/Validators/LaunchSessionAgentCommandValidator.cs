using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for LaunchSessionAgentCommand.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal sealed class LaunchSessionAgentCommandValidator : AbstractValidator<LaunchSessionAgentCommand>
{
    private const int MaxGameStateJsonLength = 50000;

    public LaunchSessionAgentCommandValidator()
    {
        RuleFor(x => x.GameSessionId)
            .NotEqual(Guid.Empty).WithMessage("GameSessionId is required");

        RuleFor(x => x.AgentDefinitionId)
            .NotEqual(Guid.Empty).WithMessage("AgentDefinitionId is required");

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty).WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEqual(Guid.Empty).WithMessage("GameId is required");

        RuleFor(x => x.InitialGameStateJson)
            .NotEmpty().WithMessage("InitialGameStateJson is required")
            .MaximumLength(MaxGameStateJsonLength)
            .WithMessage($"InitialGameStateJson cannot exceed {MaxGameStateJsonLength} characters");
    }
}
