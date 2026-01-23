using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to fetch aggregated user dashboard data (Issue #2854).
/// Returns recent games, active sessions, chat history, and library quota in a single optimized call.
/// </summary>
internal record GetUserDashboardQuery(
    Guid UserId
) : IQuery<UserDashboardDto>;
