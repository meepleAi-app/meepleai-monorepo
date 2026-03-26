using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handler for DiscardCardsCommand.
/// </summary>
public class DiscardCardsCommandHandler : IRequestHandler<DiscardCardsCommand, DiscardCardsResult>
{
    private readonly ISessionDeckRepository _deckRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<DiscardCardsCommandHandler> _logger;

    public DiscardCardsCommandHandler(
        ISessionDeckRepository deckRepository,
        ISessionRepository sessionRepository,
        IMediator mediator,
        ILogger<DiscardCardsCommandHandler> logger)
    {
        _deckRepository = deckRepository;
        _sessionRepository = sessionRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<DiscardCardsResult> Handle(DiscardCardsCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Discarding {Count} cards from deck {DeckId} for participant {ParticipantId}",
            request.CardIds.Count, request.DeckId, request.ParticipantId);

        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Deck {request.DeckId} not found.");

        if (deck.SessionId != request.SessionId)
            throw new ForbiddenException("Deck does not belong to this session.");

        // Verify participant exists in session
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found.");

        var participant = session.Participants.FirstOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new NotFoundException($"Participant {request.ParticipantId} not found in session.");

        // Get card details before discarding
        var discardedCards = deck.GetCards(request.CardIds);

        // Discard cards
        deck.DiscardCards(request.ParticipantId, request.CardIds);

        await _deckRepository.UpdateAsync(deck, cancellationToken).ConfigureAwait(false);
        await _deckRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish event (cards visible to all)
        await _mediator.Publish(new CardsDiscardedEvent
        {
            DeckId = deck.Id,
            SessionId = deck.SessionId,
            ParticipantId = request.ParticipantId,
            ParticipantName = participant.DisplayName,
            Cards = discardedCards.Select(c => new CardInfo
            {
                Id = c.Id,
                Name = c.Name,
                ImageUrl = c.ImageUrl,
                Suit = c.Suit,
                Value = c.Value
            }).ToArray()
        }, cancellationToken).ConfigureAwait(false);

        var handSize = deck.GetHand(request.ParticipantId).Count;
        _logger.LogInformation("Participant {ParticipantId} discarded {Count} cards, hand size: {HandSize}",
            request.ParticipantId, discardedCards.Count, handSize);

        return new DiscardCardsResult
        {
            DeckId = deck.Id,
            ParticipantId = request.ParticipantId,
            DiscardedCards = discardedCards.Select(c => new CardDto
            {
                Id = c.Id,
                Name = c.Name,
                ImageUrl = c.ImageUrl,
                Suit = c.Suit,
                Value = c.Value
            }).ToList(),
            HandSize = handSize
        };
    }
}
