using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Query to retrieve all active live sessions for a user.
/// Issue #4749: CQRS queries for live sessions.
/// </summary>
internal record GetUserActiveSessionsQuery(Guid UserId) : IQuery<IReadOnlyList<LiveSessionSummaryDto>>;
