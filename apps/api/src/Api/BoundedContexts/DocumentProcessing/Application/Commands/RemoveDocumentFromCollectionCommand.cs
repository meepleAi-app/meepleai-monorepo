using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to remove a PDF document from a collection.
/// Issue #2051: Remove document from collection
/// </summary>
public record RemoveDocumentFromCollectionCommand(
    Guid CollectionId,
    Guid PdfDocumentId
) : ICommand<bool>;
