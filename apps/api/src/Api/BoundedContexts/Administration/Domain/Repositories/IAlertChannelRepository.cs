using Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository contract for the <see cref="AlertChannel"/> aggregate
/// (Issue #1840 SP5 F4-C7). Channels are keyed by their <see cref="AlertChannelType"/>
/// rather than a surrogate Guid — there is at most one row per type.
/// </summary>
internal interface IAlertChannelRepository
{
    Task<IReadOnlyList<AlertChannel>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<AlertChannel?> GetByTypeAsync(AlertChannelType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a channel. When the channel already exists the implementation
    /// MUST enforce optimistic concurrency via the aggregate's
    /// <see cref="AlertChannel.Xmin"/> token (translated to
    /// <c>DbUpdateConcurrencyException</c> by EF Core).
    /// </summary>
    Task UpsertAsync(AlertChannel channel, CancellationToken cancellationToken = default);
}
