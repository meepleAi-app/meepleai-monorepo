using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Batch query to check library status for multiple games in a single request.
/// Eliminates N+1 problem when displaying game grids.
/// Issue: N+1 API calls optimization
/// </summary>
internal record BatchCheckGamesInLibraryQuery(
    Guid UserId,
    IReadOnlyList<Guid> GameIds
) : IQuery<BatchGameLibraryStatusDto>;
