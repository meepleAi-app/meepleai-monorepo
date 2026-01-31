using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to remove a document from a shared game.
/// </summary>
/// <param name="SharedGameId">The ID of the shared game</param>
/// <param name="DocumentId">The ID of the document to remove</param>
internal record RemoveDocumentFromSharedGameCommand(
    Guid SharedGameId,
    Guid DocumentId
) : ICommand;
