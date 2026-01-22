using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a contributor is added to a shared game.
/// </summary>
internal sealed class ContributorAddedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the contributor record.
    /// </summary>
    public Guid ContributorId { get; }

    /// <summary>
    /// Gets the ID of the user who became a contributor.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the ID of the shared game.
    /// </summary>
    public Guid SharedGameId { get; }

    /// <summary>
    /// Gets whether this is the primary contributor.
    /// </summary>
    public bool IsPrimary { get; }

    public ContributorAddedEvent(
        Guid contributorId,
        Guid userId,
        Guid sharedGameId,
        bool isPrimary)
    {
        ContributorId = contributorId;
        UserId = userId;
        SharedGameId = sharedGameId;
        IsPrimary = isPrimary;
    }
}

/// <summary>
/// Domain event raised when a contribution is recorded.
/// </summary>
internal sealed class ContributionRecordedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the contributor.
    /// </summary>
    public Guid ContributorId { get; }

    /// <summary>
    /// Gets the ID of the contribution record.
    /// </summary>
    public Guid ContributionId { get; }

    /// <summary>
    /// Gets the ID of the shared game.
    /// </summary>
    public Guid SharedGameId { get; }

    /// <summary>
    /// Gets the type of contribution.
    /// </summary>
    public ContributionRecordType Type { get; }

    /// <summary>
    /// Gets the version number of this contribution.
    /// </summary>
    public int Version { get; }

    public ContributionRecordedEvent(
        Guid contributorId,
        Guid contributionId,
        Guid sharedGameId,
        ContributionRecordType type,
        int version)
    {
        ContributorId = contributorId;
        ContributionId = contributionId;
        SharedGameId = sharedGameId;
        Type = type;
        Version = version;
    }
}
