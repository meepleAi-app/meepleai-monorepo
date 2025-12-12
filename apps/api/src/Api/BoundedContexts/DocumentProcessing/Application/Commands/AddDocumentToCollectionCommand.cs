using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to add a PDF document to an existing collection.
/// Issue #2051: Add document to collection with validation
/// </summary>
public record AddDocumentToCollectionCommand(
    Guid CollectionId,
    Guid PdfDocumentId,
    string DocumentType,
    int SortOrder
) : ICommand<bool>;
