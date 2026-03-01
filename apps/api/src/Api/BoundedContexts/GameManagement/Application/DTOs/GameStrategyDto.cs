namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// DTO for a game strategy entry.
/// Issue #4903: Game strategies API endpoint.
/// </summary>
public record GameStrategyDto(
    Guid Id,
    Guid GameId,
    string Title,
    string Content,
    string Author,
    int Upvotes,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt
);
