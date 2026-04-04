namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

internal record LinkKbResultDto(
    Guid VectorDocumentId,
    Guid GameId,
    Guid PdfDocumentId,
    string Status  // "linked" | "pending" (if source PDF still processing)
);
