using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to generate a setup checklist for a live session via RAG.
/// Queries the KnowledgeBase context for the game's PDF rulebook and
/// converts the streamed setup guide into a SetupChecklistData.
/// </summary>
/// <remarks>
/// <c>UserId</c> e' chi chiede la checklist, e deve essere un partecipante della sessione. La guida
/// alla preparazione legge la KB del gioco e passa dal cancello RAG (ADR-096) con l'identita'
/// dell'<b>host</b> (#4234): per una sessione conta il suo accesso. Un diniego risale come 403
/// <c>RAG_OWNERSHIP_REQUIRED</c>.
/// </remarks>
internal record GenerateSetupChecklistCommand(
    Guid SessionId,
    int PlayerCount,
    Guid UserId
) : ICommand<SetupChecklistData>;
