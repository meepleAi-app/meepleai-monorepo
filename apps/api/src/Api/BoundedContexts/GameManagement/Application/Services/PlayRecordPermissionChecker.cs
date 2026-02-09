using Api.BoundedContexts.GameManagement.Domain.Repositories;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Service for checking play record permissions.
/// Issue #3891: Authorization for play records.
/// </summary>
internal class PlayRecordPermissionChecker
{
    private readonly IPlayRecordRepository _recordRepository;

    public PlayRecordPermissionChecker(IPlayRecordRepository recordRepository)
    {
        _recordRepository = recordRepository ?? throw new ArgumentNullException(nameof(recordRepository));
    }

    /// <summary>
    /// Checks if a user can view a play record.
    /// Users can view if they are: creator, group member (for group records), or a player.
    /// </summary>
    public async Task<bool> CanViewAsync(Guid userId, Guid recordId, CancellationToken cancellationToken = default)
    {
        return await _recordRepository.CanUserViewAsync(userId, recordId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if a user can edit a play record.
    /// Only the creator can edit.
    /// </summary>
    public async Task<bool> CanEditAsync(Guid userId, Guid recordId, CancellationToken cancellationToken = default)
    {
        return await _recordRepository.CanUserEditAsync(userId, recordId, cancellationToken)
            .ConfigureAwait(false);
    }
}
