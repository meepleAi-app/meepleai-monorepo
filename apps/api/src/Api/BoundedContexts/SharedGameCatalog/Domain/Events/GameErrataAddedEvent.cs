using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when an erratum is added to a game.
/// </summary>
internal sealed class GameErrataAddedEvent : DomainEventBase
{
    public Guid GameId { get; }
    public Guid ErrataId { get; }
    public string Description { get; }

    public GameErrataAddedEvent(Guid gameId, Guid errataId, string description)
    {
        GameId = gameId;
        ErrataId = errataId;
        Description = description;
    }
}
