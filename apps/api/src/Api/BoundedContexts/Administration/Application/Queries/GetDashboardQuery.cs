using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to fetch aggregated user dashboard data for public API (Issue #3314).
/// Returns user info, stats, sessions, library snapshot, activity, and chats.
/// </summary>
public record GetDashboardQuery(
    Guid UserId
) : IQuery<DashboardResponseDto>;
