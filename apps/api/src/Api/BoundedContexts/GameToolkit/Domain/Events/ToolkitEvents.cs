using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameToolkit.Domain.Events;

internal sealed class ToolkitCreatedEvent : DomainEventBase
{
    public Guid ToolkitId { get; }
    public Guid GameId { get; }
    public string Name { get; }

    public ToolkitCreatedEvent(Guid toolkitId, Guid gameId, string name)
    {
        ToolkitId = toolkitId;
        GameId = gameId;
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
