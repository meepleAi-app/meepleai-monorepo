namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO for exported chat thread data.
/// </summary>
public record ExportedChatDto(
    string Format,
    string Content,
    string ContentType,
    string FileExtension,
    DateTime ExportedAt
);
