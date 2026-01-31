namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Request DTO for adding a document to an existing collection.
/// Issue #2051: Add document to collection operation
/// </summary>
internal record AddDocumentToCollectionRequest(
    Guid PdfDocumentId,
    string DocumentType,
    int SortOrder
);
