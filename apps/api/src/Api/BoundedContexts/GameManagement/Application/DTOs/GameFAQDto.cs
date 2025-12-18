#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.BoundedContexts.GameManagement.Application.DTOs;

/// <summary>
/// DTO for GameFAQ entity.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal record GameFAQDto(
    Guid Id,
    Guid GameId,
    string Question,
    string Answer,
    int Upvotes,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>
/// DTO for creating a GameFAQ.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal record CreateGameFAQRequest(
    string Question,
    string Answer
);

/// <summary>
/// DTO for updating a GameFAQ.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal record UpdateGameFAQRequest(
    string Question,
    string Answer
);
