using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Query to retrieve full live session details by ID.
/// Issue #4749: CQRS queries for live sessions.
/// </summary>
internal record GetLiveSessionQuery(Guid SessionId) : IQuery<LiveSessionDto>;
