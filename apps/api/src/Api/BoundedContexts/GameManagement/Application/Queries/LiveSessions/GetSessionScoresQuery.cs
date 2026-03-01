using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Query to retrieve scores for a live session.
/// Issue #4749: CQRS queries for live sessions.
/// </summary>
internal record GetSessionScoresQuery(Guid SessionId) : IQuery<IReadOnlyList<LiveSessionRoundScoreDto>>;
