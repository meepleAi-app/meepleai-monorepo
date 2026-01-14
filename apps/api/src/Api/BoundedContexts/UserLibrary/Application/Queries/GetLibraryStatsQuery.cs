using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to get library statistics for a user.
/// </summary>
internal record GetLibraryStatsQuery(
    Guid UserId
) : IQuery<UserLibraryStatsDto>;
