using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Query to retrieve players in a live session.
/// Issue #4749: CQRS queries for live sessions.
/// </summary>
internal record GetSessionPlayersQuery(Guid SessionId) : IQuery<IReadOnlyList<LiveSessionPlayerDto>>;
