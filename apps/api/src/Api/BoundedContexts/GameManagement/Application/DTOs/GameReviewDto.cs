namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// DTO for a game review entry.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
public record GameReviewDto(
    Guid Id,
    Guid GameId,
    string AuthorName,
    int Rating,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
