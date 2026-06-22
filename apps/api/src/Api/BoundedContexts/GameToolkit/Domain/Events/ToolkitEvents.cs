using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameToolkit.Domain.Events;

internal sealed class ToolkitCreatedEvent : DomainEventBase
{
    public Guid ToolkitId { get; }
    public Guid? GameId { get; }
    public Guid? PrivateGameId { get; }
    public string Name { get; }

    public ToolkitCreatedEvent(Guid toolkitId, Guid? gameId, Guid? privateGameId, string name)
    {
        ToolkitId = toolkitId;
        GameId = gameId;
        PrivateGameId = privateGameId;
        Name = name;
    }
}

internal sealed class ToolkitUpdatedEvent : DomainEventBase
{
    public Guid ToolkitId { get; }
    public string Name { get; }

    public ToolkitUpdatedEvent(Guid toolkitId, string name)
    {
        ToolkitId = toolkitId;
        Name = name;
    }
}

internal sealed class ToolkitPublishedEvent : DomainEventBase
{
    public Guid ToolkitId { get; }
    public int Version { get; }

    public ToolkitPublishedEvent(Guid toolkitId, int version)
    {
        ToolkitId = toolkitId;
        Version = version;
    }
}

internal sealed class TimerExpiredEvent : DomainEventBase
{
    public Guid ToolkitId { get; }
    public string TimerName { get; }

    public TimerExpiredEvent(Guid toolkitId, string timerName)
    {
        ToolkitId = toolkitId;
        TimerName = timerName;
    }
}

/// <summary>
/// Raised when a new <c>ToolkitVersion</c> is published for a <c>GameToolkit</c>.
/// Consumers invalidate <c>toolkit:{id}</c>, <c>toolkits:{id}:versions</c>,
/// <c>toolkits:popular</c> cache tags per the matrix in issue #822 (spec-panel 2026-05-18).
/// </summary>
internal sealed class ToolkitVersionPublishedEvent : DomainEventBase
{
    public Guid ToolkitId { get; }
    public Guid VersionId { get; }
    public string VersionNumber { get; }
    public Guid PublishedBy { get; }

    public ToolkitVersionPublishedEvent(Guid toolkitId, Guid versionId, string versionNumber, Guid publishedBy)
    {
        ToolkitId = toolkitId;
        VersionId = versionId;
        VersionNumber = versionNumber;
        PublishedBy = publishedBy;
    }
}

/// <summary>
/// Raised when a published <c>ToolkitVersion</c> is yanked (soft-delete + audit).
/// Consumers invalidate <c>toolkit:{id}</c>, <c>toolkits:{id}:versions</c>,
/// <c>toolkits:popular</c>, and <c>toolkit:{id}:ratings</c> cache tags. The
/// yank-of-last-version cascade (toolkit-unpublished) is handled at command level,
/// not by this event.
/// </summary>
internal sealed class ToolkitVersionYankedEvent : DomainEventBase
{
    public Guid ToolkitId { get; }
    public Guid VersionId { get; }
    public string VersionNumber { get; }
    public Guid YankedBy { get; }
    public string Reason { get; }

    public ToolkitVersionYankedEvent(Guid toolkitId, Guid versionId, string versionNumber, Guid yankedBy, string reason)
    {
        ToolkitId = toolkitId;
        VersionId = versionId;
        VersionNumber = versionNumber;
        YankedBy = yankedBy;
        Reason = reason;
    }
}
