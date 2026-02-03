using Api.BoundedContexts.Administration.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to retrieve user library statistics (admin view).
/// Issue #3139 - Admin endpoint to view any user's library stats.
/// </summary>
/// <param name="UserId">User ID to fetch library stats for</param>
internal sealed record GetUserLibraryStatsQuery(
    Guid UserId
) : IRequest<AdminUserLibraryStatsDto?>;
