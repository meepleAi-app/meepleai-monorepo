using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for LaunchSessionAgentCommand.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// Issue #2500: Added semantic validations — V1 exists, V2 active, V3 game-match, V4 JSON safe-parse.
/// All FluentValidation failures produce HTTP 422 (validation_error) per codebase convention.
/// </summary>
internal sealed class LaunchSessionAgentCommandValidator : AbstractValidator<LaunchSessionAgentCommand>
{
    private const int MaxGameStateJsonLength = 50000;

    public LaunchSessionAgentCommandValidator(IAgentDefinitionRepository agentDefinitionRepository)
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

        // V4 — JSON safe-parse: validate before async repo rules (sync, no I/O)
        RuleFor(x => x.InitialGameStateJson)
            .Must(json =>
            {
                try
                {
                    GameState.FromJson(json);
                    return true;
                }
                catch
                {
                    return false;
                }
            })
            .WithMessage("InitialGameStateJson is not a valid game state.")
            .When(x => !string.IsNullOrWhiteSpace(x.InitialGameStateJson));

        // V1 / V2 / V3 — repository-backed async rules
        // All gated so they only run when AgentDefinitionId is a valid (non-empty) guid
        RuleFor(x => x.AgentDefinitionId)
            .MustAsync(async (command, agentDefinitionId, ct) =>
            {
                var definition = await agentDefinitionRepository
                    .GetByIdAsync(agentDefinitionId, ct)
                    .ConfigureAwait(false);
                return definition is not null;
            })
            .WithMessage("AgentDefinition not found or has been deleted.")
            .When(x => x.AgentDefinitionId != Guid.Empty);

        RuleFor(x => x.AgentDefinitionId)
            .MustAsync(async (command, agentDefinitionId, ct) =>
            {
                var definition = await agentDefinitionRepository
                    .GetByIdAsync(agentDefinitionId, ct)
                    .ConfigureAwait(false);
                return definition is { IsActive: true };
            })
            .WithMessage("AgentDefinition is not active.")
            .When(x => x.AgentDefinitionId != Guid.Empty);

        RuleFor(x => x.AgentDefinitionId)
            .MustAsync(async (command, agentDefinitionId, ct) =>
            {
                var definition = await agentDefinitionRepository
                    .GetByIdAsync(agentDefinitionId, ct)
                    .ConfigureAwait(false);
                return definition is not null && definition.GameId == command.GameId;
            })
            .WithMessage("AgentDefinition does not belong to the specified game.")
            .When(x => x.AgentDefinitionId != Guid.Empty);
    }
}
