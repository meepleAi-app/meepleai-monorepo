using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;

/// <summary>
/// Handles retrieving the version history of a play record.
/// Creator-only: gates on <see cref="PlayRecordPermissionChecker.CanEditAsync"/>.
/// Returns the last 5 versions ordered by VersionNumber descending.
/// Issue #2437-3: version history + restore.
/// </summary>
internal sealed class GetPlayRecordVersionsQueryHandler
    : IQueryHandler<GetPlayRecordVersionsQuery, IReadOnlyList<PlayRecordVersionDto>>
{
    private readonly IPlayRecordVersionRepository _versionRepository;
    private readonly PlayRecordPermissionChecker _permissionChecker;

    public GetPlayRecordVersionsQueryHandler(
        IPlayRecordVersionRepository versionRepository,
        PlayRecordPermissionChecker permissionChecker)
    {
        _versionRepository = versionRepository ?? throw new ArgumentNullException(nameof(versionRepository));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
    }

    public async Task<IReadOnlyList<PlayRecordVersionDto>> Handle(
        GetPlayRecordVersionsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!await _permissionChecker.CanEditAsync(query.UserId, query.RecordId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ForbiddenException("You do not have permission to view the version history of this play record.");
        }

        var versions = await _versionRepository
            .GetRecentAsync(query.RecordId, 5, cancellationToken)
            .ConfigureAwait(false);

        return versions
            .Select(v => new PlayRecordVersionDto(
                v.VersionNumber,
                v.SessionDate,
                v.Notes,
                v.Location,
                v.CreatedAt,
                v.CreatedByUserId))
            .ToList();
    }
}
