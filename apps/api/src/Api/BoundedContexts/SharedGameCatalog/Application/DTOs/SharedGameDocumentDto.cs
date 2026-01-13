using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO for SharedGameDocument.
/// </summary>
public record SharedGameDocumentDto(
    Guid Id,
    Guid SharedGameId,
    Guid PdfDocumentId,
    SharedGameDocumentType DocumentType,
    string Version,
    bool IsActive,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt,
    Guid CreatedBy
);
