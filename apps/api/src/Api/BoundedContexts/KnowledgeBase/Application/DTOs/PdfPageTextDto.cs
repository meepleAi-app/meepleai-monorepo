namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

public sealed record PdfPageTextDto(
    int PageNumber,
    string Text,
    string DocumentTitle,
    int TotalPages
);
