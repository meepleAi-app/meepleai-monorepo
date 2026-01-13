using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to add a document to a shared game.
/// </summary>
/// <param name="SharedGameId">The ID of the shared game</param>
/// <param name="PdfDocumentId">The ID of the PDF document</param>
/// <param name="DocumentType">The type of document (Rulebook, Errata, Homerule)</param>
/// <param name="Version">The version string (e.g., "1.0", "2.1")</param>
/// <param name="Tags">Optional tags for Homerule documents</param>
/// <param name="SetAsActive">Whether to set this document as the active version</param>
internal record AddDocumentToSharedGameCommand(
    Guid SharedGameId,
    Guid PdfDocumentId,
    SharedGameDocumentType DocumentType,
    string Version,
    List<string>? Tags,
    bool SetAsActive
) : ICommand<Guid>; // Returns the document ID
