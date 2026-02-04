namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO representing a game label.
/// </summary>
internal record LabelDto(
    Guid Id,
    string Name,
    string Color,
    bool IsPredefined,
    DateTime CreatedAt
);
