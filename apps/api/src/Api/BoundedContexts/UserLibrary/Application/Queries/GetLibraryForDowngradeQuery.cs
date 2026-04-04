using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to preview which library entries would be kept vs removed when downgrading to a new quota.
/// Returns entries sorted by priority: favorites first, then by times played, then by last played / added date.
/// </summary>
internal record GetLibraryForDowngradeQuery(
    Guid UserId,
    int NewQuota
) : IQuery<LibraryForDowngradeDto>;
