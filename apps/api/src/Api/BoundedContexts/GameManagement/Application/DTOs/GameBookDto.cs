namespace Api.BoundedContexts.GameManagement.Application.DTOs;

public sealed record GameBookDto(
    Guid Id,
    Guid GameRefId,
    int GameRefKind,
    Guid? OwnerUserId,
    string DisplayName,
    int Roles,
    int ParagraphScheme,
    string Language,
    bool SequentialRead,
    Guid? KbSourceDocId,
    bool PhysicalOnly,
    DateTimeOffset CreatedAt);
