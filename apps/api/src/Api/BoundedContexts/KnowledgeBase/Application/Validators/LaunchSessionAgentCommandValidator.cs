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

        // C1 fix: InitialGameStateJson is now optional — empty/whitespace means "use server default".
        // Only MaximumLength applies unconditionally; V4 JSON parse is gated on non-empty.
        RuleFor(x => x.InitialGameStateJson)
            .MaximumLength(MaxGameStateJsonLength)
            .WithMessage($"InitialGameStateJson cannot exceed {MaxGameStateJsonLength} characters");

        // V4 — JSON safe-parse: only when non-empty (empty = use GameState.Initial on the handler side)
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

        // V1 / V2 / V3 — consolidated into ONE async rule with a SINGLE DB query (I2 fix).
        // Previously three separate RuleFor rules each called GetByIdAsync → 3 queries + 3 errors
        // for a missing definition.  Now: one query, fail-fast on first problem with a targeted message.
        // CustomAsync gives access to ValidationContext<T> so we can name the property explicitly.
        RuleFor(x => x)
            .CustomAsync(async (command, ctx, ct) =>
            {
                if (command.AgentDefinitionId == Guid.Empty)
                    return; // gated below via When; also checked separately by NotEqual rule

                var definition = await agentDefinitionRepository
                    .GetByIdAsync(command.AgentDefinitionId, ct)
                    .ConfigureAwait(false);

                // V1 — exists
                if (definition is null)
                {
                    ctx.AddFailure("AgentDefinitionId", "AgentDefinition not found or has been deleted.");
                    return;
                }

                // V2 — active
                if (!definition.IsActive)
                {
                    ctx.AddFailure("AgentDefinitionId", "AgentDefinition is not active.");
                    return;
                }

                // V3 — game-match
                if (definition.GameId != command.GameId)
                {
                    ctx.AddFailure("AgentDefinitionId", "AgentDefinition does not belong to the specified game.");
                }
            })
            .When(x => x.AgentDefinitionId != Guid.Empty);
    }
}
