using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for LaunchSessionAgentCommand.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// Issue #2500: Added semantic validations — V1 exists, V2 active, V4 JSON safe-parse.
/// Issue #4154: V3 (game-match) tolta e AgentDefinitionId opzionale (ADR-094: un solo agente di sistema).
/// All FluentValidation failures produce HTTP 422 (validation_error) per codebase convention.
/// </summary>
internal sealed class LaunchSessionAgentCommandValidator : AbstractValidator<LaunchSessionAgentCommand>
{
    private const int MaxGameStateJsonLength = 50000;

    public LaunchSessionAgentCommandValidator(IAgentDefinitionRepository agentDefinitionRepository)
    {
        RuleFor(x => x.GameSessionId)
            .NotEqual(Guid.Empty).WithMessage("GameSessionId is required");

        // Issue #4154: opzionale (assente = agente di sistema, risolto dall'handler); se c'e`,
        // non puo` essere vuoto.
        RuleFor(x => x.AgentDefinitionId)
            .NotEqual(Guid.Empty).WithMessage("AgentDefinitionId, when provided, cannot be empty")
            .When(x => x.AgentDefinitionId.HasValue);

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

        // V1 / V2 — consolidated into ONE async rule with a SINGLE DB query (I2 fix).
        // CustomAsync gives access to ValidationContext<T> so we can name the property explicitly.
        //
        // Issue #4154: la regola V3 («l'agente deve appartenere al gioco») e` stata tolta. Con ADR-094
        // l'agente e` uno solo, di sistema, con GameId nullo: V3 lo bocciava per QUALUNQUE gioco, e
        // l'assistente in sessione non poteva partire. L'ambito del recupero e` un parametro della
        // domanda, non dell'agente.
        RuleFor(x => x)
            .CustomAsync(async (command, ctx, ct) =>
            {
                if (command.AgentDefinitionId is not { } agentDefinitionId || agentDefinitionId == Guid.Empty)
                    return; // assente: ci pensa l'handler; vuoto: lo segnala la regola NotEqual

                var definition = await agentDefinitionRepository
                    .GetByIdAsync(agentDefinitionId, ct)
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
                }
            });
    }
}
