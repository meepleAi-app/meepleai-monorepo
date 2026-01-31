using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

/// <summary>
/// Domain event raised when a FAQ is added to a game.
/// </summary>
internal sealed class GameFaqAddedEvent : DomainEventBase
{
    public Guid GameId { get; }
    public Guid FaqId { get; }
    public string Question { get; }

    public GameFaqAddedEvent(Guid gameId, Guid faqId, string question)
    {
        GameId = gameId;
        FaqId = faqId;
        Question = question;
    }
}
