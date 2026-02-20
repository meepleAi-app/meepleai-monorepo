using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Query to retrieve a live session by its join code.
/// Issue #4749: CQRS queries for live sessions.
/// </summary>
internal record GetLiveSessionByCodeQuery(string SessionCode) : IQuery<LiveSessionDto>;
