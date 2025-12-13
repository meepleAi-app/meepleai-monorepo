using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// DTO for DocumentCollection with list of documents.
/// Issue #2051: Multi-document collection response
/// </summary>
public record DocumentCollectionDto(
    Guid Id,
    Guid GameId,
    string Name,
    string? Description,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CollectionDocumentDto> Documents,
    int DocumentCount,
    bool IsFull
);

/// <summary>
/// DTO for a document within a collection.
/// </summary>
public record CollectionDocumentDto(
    Guid PdfDocumentId,
    string DocumentType,
    int SortOrder,
    DateTime AddedAt,
    PdfDocumentDto? PdfDocument = null
);
