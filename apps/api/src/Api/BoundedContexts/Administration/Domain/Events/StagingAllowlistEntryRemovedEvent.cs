using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.Administration.Domain.Events;

/// <summary>
/// Emitted when an entry is soft-deleted from the staging allowlist.
/// Consumed by <c>StagingAccessGuardCacheInvalidator</c> to flush the in-memory cache.
/// </summary>
public sealed record StagingAllowlistEntryRemovedEvent(
    Guid EntryId,
    string Email,
    Guid? RemovedByUserId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();
}
