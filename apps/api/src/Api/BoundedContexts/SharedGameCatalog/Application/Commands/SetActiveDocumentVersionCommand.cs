using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to set a document version as active.
/// Automatically deactivates other versions of the same type.
/// </summary>
/// <param name="SharedGameId">The ID of the shared game</param>
/// <param name="DocumentId">The ID of the document to activate</param>
internal record SetActiveDocumentVersionCommand(
    Guid SharedGameId,
    Guid DocumentId
) : ICommand;
