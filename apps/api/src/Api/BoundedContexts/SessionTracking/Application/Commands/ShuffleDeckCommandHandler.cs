using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Handler for ShuffleDeckCommand.
/// </summary>
public class ShuffleDeckCommandHandler : IRequestHandler<ShuffleDeckCommand, ShuffleDeckResult>
{
    private readonly ISessionDeckRepository _deckRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<ShuffleDeckCommandHandler> _logger;

    public ShuffleDeckCommandHandler(
        ISessionDeckRepository deckRepository,
        IMediator mediator,
        ILogger<ShuffleDeckCommandHandler> logger)
    {
        _deckRepository = deckRepository;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<ShuffleDeckResult> Handle(ShuffleDeckCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shuffling deck {DeckId}, includeDiscard: {IncludeDiscard}",
            request.DeckId, request.IncludeDiscard);

        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Deck {request.DeckId} not found.");

        if (deck.SessionId != request.SessionId)
            throw new ForbiddenException("Deck does not belong to this session.");

        if (request.IncludeDiscard)
        {
            deck.ShuffleDiscardIntoDraw();
        }
        else
        {
            deck.Shuffle();
        }

        await _deckRepository.UpdateAsync(deck, cancellationToken).ConfigureAwait(false);
        await _deckRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish event
        await _mediator.Publish(new DeckShuffledEvent
        {
            DeckId = deck.Id,
            SessionId = deck.SessionId,
            DeckName = deck.Name,
            CardsInDrawPile = deck.DrawPile.Count
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Shuffled deck {DeckId}, cards in draw pile: {Count}",
            deck.Id, deck.DrawPile.Count);

        return new ShuffleDeckResult
        {
            DeckId = deck.Id,
            CardsInDrawPile = deck.DrawPile.Count,
            ShuffledAt = deck.LastShuffledAt ?? DateTime.UtcNow
        };
    }
}
