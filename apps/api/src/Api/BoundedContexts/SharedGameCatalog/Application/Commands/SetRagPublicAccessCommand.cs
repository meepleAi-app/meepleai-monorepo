using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to toggle IsRagPublic on a SharedGame.
/// Admin-only. When true, any user can access the game's RAG content without declaring ownership.
/// </summary>
internal sealed record SetRagPublicAccessCommand(
    Guid SharedGameId,
    bool IsRagPublic
) : ICommand;
