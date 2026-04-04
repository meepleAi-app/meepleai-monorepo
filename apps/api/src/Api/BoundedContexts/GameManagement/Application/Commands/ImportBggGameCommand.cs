using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to import a BGG game as a PrivateGame and add it to the user's library.
/// Game Night Improvvisata - E1-2: Import BGG game with tier enforcement.
/// </summary>
internal sealed record ImportBggGameCommand(
    Guid UserId,
    int BggId
) : ICommand<ImportBggGameResult>;

/// <summary>
/// Result returned after a successful BGG game import.
/// </summary>
internal sealed record ImportBggGameResult(
    Guid PrivateGameId,
    Guid LibraryEntryId,
    string Title,
    string? ImageUrl
);
