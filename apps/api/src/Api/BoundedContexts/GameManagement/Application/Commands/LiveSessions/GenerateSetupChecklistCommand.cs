using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Command to generate a setup checklist for a live session via RAG.
/// Queries the KnowledgeBase context for the game's PDF rulebook and
/// converts the streamed setup guide into a SetupChecklistData.
/// </summary>
internal record GenerateSetupChecklistCommand(
    Guid SessionId,
    int PlayerCount
) : ICommand<SetupChecklistData>;
