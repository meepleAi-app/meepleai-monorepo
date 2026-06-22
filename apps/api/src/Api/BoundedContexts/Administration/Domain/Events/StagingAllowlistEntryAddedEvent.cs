using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.Administration.Domain.Events;

/// <summary>
/// Emitted when an entry is added to the staging allowlist.
/// Consumed by <c>StagingAccessGuardCacheInvalidator</c> to flush the in-memory cache.
/// </summary>
public sealed record StagingAllowlistEntryAddedEvent(
    Guid EntryId,
    string Email,
    Guid? AddedByUserId) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public Guid EventId { get; } = Guid.NewGuid();
}
