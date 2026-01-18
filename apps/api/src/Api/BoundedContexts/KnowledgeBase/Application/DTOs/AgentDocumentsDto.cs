using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO representing agent's selected documents for knowledge base.
/// Issue #2399: Knowledge Base Document Selection.
/// </summary>
public record AgentDocumentsDto(
    Guid AgentId,
    IReadOnlyList<SelectedDocumentDto> Documents
);

/// <summary>
/// DTO for a selected document with metadata.
/// </summary>
public record SelectedDocumentDto(
    Guid Id,
    Guid SharedGameId,
    Guid PdfDocumentId,
    SharedGameDocumentType DocumentType,
    string Version,
    bool IsActive,
    IReadOnlyList<string> Tags,
    string? GameName
);
