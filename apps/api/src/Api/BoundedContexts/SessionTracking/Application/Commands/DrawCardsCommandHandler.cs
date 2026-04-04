using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handler for DrawCardsCommand.
/// </summary>
public class DrawCardsCommandHandler : IRequestHandler<DrawCardsCommand, DrawCardsResult>
{
    private readonly ISessionDeckRepository _deckRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<DrawCardsCommandHandler> _logger;

    public DrawCardsCommandHandler(
        ISessionDeckRepository deckRepository,
        ISessionRepository sessionRepository,
        IMediator mediator,
        ILogger<DrawCardsCommandHandler> logger)
    {
        _deckRepository = deckRepository;
        _sessionRepository = sessionRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<DrawCardsResult> Handle(DrawCardsCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Drawing {Count} cards from deck {DeckId} for participant {ParticipantId}",
            request.Count, request.DeckId, request.ParticipantId);

        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Deck {request.DeckId} not found.");

        if (deck.SessionId != request.SessionId)
            throw new ForbiddenException("Deck does not belong to this session.");

        // Verify participant exists in session
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found.");

        var participant = session.Participants.FirstOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new NotFoundException($"Participant {request.ParticipantId} not found in session.");

        // Draw cards
        var drawnCardIds = deck.DrawCards(request.ParticipantId, request.Count);
        var drawnCards = deck.GetCards(drawnCardIds);

        await _deckRepository.UpdateAsync(deck, cancellationToken).ConfigureAwait(false);
        await _deckRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish event (cards hidden from other participants)
        await _mediator.Publish(new CardsDrawnEvent
        {
            DeckId = deck.Id,
            SessionId = deck.SessionId,
            ParticipantId = request.ParticipantId,
            ParticipantName = participant.DisplayName,
            CardCount = drawnCards.Count,
            CardIds = drawnCardIds.ToArray(),
            RemainingCards = deck.DrawPile.Count
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Participant {ParticipantId} drew {Count} cards, remaining: {Remaining}",
            request.ParticipantId, drawnCards.Count, deck.DrawPile.Count);

        return new DrawCardsResult
        {
            DeckId = deck.Id,
            ParticipantId = request.ParticipantId,
            Cards = drawnCards.Select(c => new CardDto
            {
                Id = c.Id,
                Name = c.Name,
                ImageUrl = c.ImageUrl,
                Suit = c.Suit,
                Value = c.Value
            }).ToList(),
            RemainingCards = deck.DrawPile.Count
        };
    }
}
