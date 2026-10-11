using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to generate a setup checklist for a live session via RAG.
/// Queries the KnowledgeBase context for the game's PDF rulebook and
/// converts the streamed setup guide into a SetupChecklistData.
/// </summary>
/// <remarks>
/// #4206: <c>UserId</c> e <c>UserRole</c> sono di chi chiede la checklist. La guida alla preparazione
/// legge la KB del gioco, quindi passa dal cancello RAG (ADR-096): un diniego risale come 403
/// <c>RAG_OWNERSHIP_REQUIRED</c>.
/// </remarks>
internal record GenerateSetupChecklistCommand(
    Guid SessionId,
    int PlayerCount,
    Guid UserId,
    string UserRole
) : ICommand<SetupChecklistData>;
