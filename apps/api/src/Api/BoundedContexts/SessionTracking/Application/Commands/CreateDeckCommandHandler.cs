using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handler for CreateDeckCommand.
/// </summary>
public class CreateDeckCommandHandler : IRequestHandler<CreateDeckCommand, CreateDeckResult>
{
    private readonly ISessionDeckRepository _deckRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<CreateDeckCommandHandler> _logger;

    public CreateDeckCommandHandler(
        ISessionDeckRepository deckRepository,
        IMediator mediator,
        ILogger<CreateDeckCommandHandler> logger)
    {
        _deckRepository = deckRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<CreateDeckResult> Handle(CreateDeckCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating deck for session {SessionId}, type: {DeckType}",
            request.SessionId, request.DeckType);

        SessionDeck deck;

        if (string.Equals(request.DeckType, "standard", StringComparison.Ordinal))
        {
            deck = SessionDeck.CreateStandardDeck(
                request.SessionId,
                request.Name,
                request.IncludeJokers);
        }
        else
        {
            var customCards = request.CustomCards!
                .Select((c, i) => Card.Create(c.Name, c.ImageUrl, c.Suit, c.Value, i))
                .ToList();

            deck = SessionDeck.CreateCustomDeck(
                request.SessionId,
                request.Name,
                customCards);
        }

        await _deckRepository.AddAsync(deck, cancellationToken).ConfigureAwait(false);
        await _deckRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish event
        await _mediator.Publish(new DeckCreatedEvent
        {
            DeckId = deck.Id,
            SessionId = deck.SessionId,
            DeckName = deck.Name,
            DeckType = deck.DeckType.ToString(),
            CardCount = deck.Cards.Count
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Created deck {DeckId} with {CardCount} cards",
            deck.Id, deck.Cards.Count);

        return new CreateDeckResult
        {
            DeckId = deck.Id,
            Name = deck.Name,
            DeckType = deck.DeckType.ToString(),
            CardCount = deck.Cards.Count,
            CreatedAt = deck.CreatedAt
        };
    }
}
