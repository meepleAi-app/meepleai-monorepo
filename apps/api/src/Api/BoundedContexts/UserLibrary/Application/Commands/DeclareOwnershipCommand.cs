using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to declare explicit ownership of a game in the user's library.
/// Grants RAG access to the game's knowledge base.
/// Idempotent: if already declared, returns current state without error.
/// </summary>
internal sealed record DeclareOwnershipCommand(
    Guid UserId,
    Guid GameId
) : ICommand<DeclareOwnershipResult>;

/// <summary>
/// Result of the declare-ownership operation.
/// </summary>
internal sealed record DeclareOwnershipResult(
    string GameState,
    DateTime? OwnershipDeclaredAt,
    bool HasRagAccess,
    int KbCardCount,
    bool IsRagPublic
);
