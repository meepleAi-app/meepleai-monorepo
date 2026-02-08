using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository interface for PlayRecord aggregate.
/// Issue #3889: CQRS commands for play record management.
/// </summary>
internal interface IPlayRecordRepository : IRepository<PlayRecord, Guid>
{
    /// <summary>
    /// Gets a play record by ID including all players and scores.
    /// </summary>
    Task<PlayRecord?> GetByIdWithPlayersAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets paginated play history for a user.
    /// </summary>
    Task<(IReadOnlyList<PlayRecord> Records, int Total)> GetUserHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        Guid? gameId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has permission to view a play record.
    /// </summary>
    Task<bool> CanUserViewAsync(Guid userId, Guid recordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has permission to edit a play record.
    /// </summary>
    Task<bool> CanUserEditAsync(Guid userId, Guid recordId, CancellationToken cancellationToken = default);
}
