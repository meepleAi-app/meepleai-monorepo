using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to create a new document collection with optional initial documents.
/// Issue #2051: Multi-document collection creation
/// </summary>
internal record CreateDocumentCollectionCommand(
    Guid GameId,
    Guid UserId,
    string Name,
    string? Description,
    IReadOnlyList<InitialDocumentRequest> InitialDocuments
) : ICommand<DocumentCollectionDto>;
