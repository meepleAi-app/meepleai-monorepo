using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handles a role-validated card draw player action.
/// Delegates to the existing deck domain logic and broadcasts a CardsDrawnEvent via SSE.
/// Issue #4765 - Player Action Endpoints
/// </summary>
public class DrawSessionCardCommandHandler : IRequestHandler<DrawSessionCardCommand, DrawSessionCardResult>
{
    private readonly ISessionDeckRepository _deckRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionSyncService _syncService;

    public DrawSessionCardCommandHandler(
        ISessionDeckRepository deckRepository,
        ISessionRepository sessionRepository,
        ISessionSyncService syncService)
    {
        _deckRepository = deckRepository ?? throw new ArgumentNullException(nameof(deckRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
    }

    public async Task<DrawSessionCardResult> Handle(DrawSessionCardCommand request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Deck {request.DeckId} not found");

        if (deck.SessionId != request.SessionId)
            throw new ForbiddenException("Deck does not belong to this session.");

        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        if (session.Status != SessionStatus.Active)
            throw new ConflictException($"Cannot draw cards for session with status {session.Status}");

        var participant = session.Participants.FirstOrDefault(p => p.Id == request.ParticipantId)
            ?? throw new NotFoundException($"Participant {request.ParticipantId} not found in session");

        var drawnCardIds = deck.DrawCards(request.ParticipantId, request.Count);
        var drawnCards = deck.GetCards(drawnCardIds);

        await _deckRepository.UpdateAsync(deck, cancellationToken).ConfigureAwait(false);
        await _deckRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _syncService.PublishEventAsync(deck.SessionId, new CardsDrawnEvent
        {
            DeckId = deck.Id,
            SessionId = deck.SessionId,
            ParticipantId = request.ParticipantId,
            ParticipantName = participant.DisplayName,
            CardCount = drawnCards.Count,
            CardIds = drawnCardIds.ToArray(),
            RemainingCards = deck.DrawPile.Count
        }, cancellationToken).ConfigureAwait(false);

        return new DrawSessionCardResult(
            deck.Id,
            request.ParticipantId,
            drawnCards.Select(c => new CardDrawDto(c.Id, c.Name, c.ImageUrl, c.Suit, c.Value)).ToList(),
            deck.DrawPile.Count);
    }
}
