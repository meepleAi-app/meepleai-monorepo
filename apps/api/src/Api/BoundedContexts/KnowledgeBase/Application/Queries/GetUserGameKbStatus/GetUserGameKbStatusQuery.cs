using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetUserGameKbStatus;

/// <summary>
/// Query to retrieve the KB indexing status for a specific game as seen by a regular user.
/// KB-03: Returns document count, coverage score, coverage level, and suggested questions.
/// </summary>
internal sealed record GetUserGameKbStatusQuery(Guid GameId)
    : IQuery<UserGameKbStatusDto>;

/// <summary>
/// DTO representing the KB status for a game.
/// </summary>
internal sealed record UserGameKbStatusDto(
    Guid GameId,
    bool IsIndexed,
    int DocumentCount,
    int CoverageScore,
    string CoverageLevel,
    List<string> SuggestedQuestions);
