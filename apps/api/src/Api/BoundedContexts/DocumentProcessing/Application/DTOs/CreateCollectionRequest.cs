namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Request DTO for creating a new document collection.
/// Issue #2051: Multi-document collection creation
/// </summary>
internal record CreateCollectionRequest(
    string Name,
    string? Description,
    IReadOnlyList<InitialDocumentRequest> InitialDocuments
);

/// <summary>
/// Initial document to add when creating collection.
/// </summary>
internal record InitialDocumentRequest(
    Guid PdfDocumentId,
    string DocumentType,
    int SortOrder
);
