using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Handler for GetSessionDecksQuery.
/// </summary>
public class GetSessionDecksQueryHandler : IRequestHandler<GetSessionDecksQuery, List<SessionDeckDto>>
{
    private readonly ISessionDeckRepository _deckRepository;

    public GetSessionDecksQueryHandler(ISessionDeckRepository deckRepository)
    {
        _deckRepository = deckRepository;
    }

    public async Task<List<SessionDeckDto>> Handle(GetSessionDecksQuery request, CancellationToken cancellationToken)
    {
        var decks = await _deckRepository.GetBySessionIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false);

        return decks.Select(d => new SessionDeckDto
        {
            Id = d.Id,
            Name = d.Name,
            DeckType = d.DeckType.ToString(),
            TotalCards = d.Cards.Count,
            CardsInDrawPile = d.DrawPile.Count,
            CardsInDiscardPile = d.DiscardPile.Count,
            CreatedAt = d.CreatedAt,
            LastShuffledAt = d.LastShuffledAt
        }).ToList();
    }
}

/// <summary>
/// Handler for GetPlayerHandQuery.
/// </summary>
public class GetPlayerHandQueryHandler : IRequestHandler<GetPlayerHandQuery, PlayerHandDto>
{
    private readonly ISessionDeckRepository _deckRepository;

    public GetPlayerHandQueryHandler(ISessionDeckRepository deckRepository)
    {
        _deckRepository = deckRepository;
    }

    public async Task<PlayerHandDto> Handle(GetPlayerHandQuery request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Deck {request.DeckId} not found.");

        if (deck.SessionId != request.SessionId)
            throw new ForbiddenException("Deck does not belong to this session.");

        var handCardIds = deck.GetHand(request.ParticipantId);
        var handCards = deck.GetCards(handCardIds);

        return new PlayerHandDto
        {
            DeckId = deck.Id,
            ParticipantId = request.ParticipantId,
            Cards = handCards.Select(c => new CardDto
            {
                Id = c.Id,
                Name = c.Name,
                ImageUrl = c.ImageUrl,
                Suit = c.Suit,
                Value = c.Value
            }).ToList()
        };
    }
}

/// <summary>
/// Handler for GetDiscardPileQuery.
/// </summary>
public class GetDiscardPileQueryHandler : IRequestHandler<GetDiscardPileQuery, DiscardPileDto>
{
    private readonly ISessionDeckRepository _deckRepository;

    public GetDiscardPileQueryHandler(ISessionDeckRepository deckRepository)
    {
        _deckRepository = deckRepository;
    }

    public async Task<DiscardPileDto> Handle(GetDiscardPileQuery request, CancellationToken cancellationToken)
    {
        var deck = await _deckRepository.GetByIdAsync(request.DeckId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Deck {request.DeckId} not found.");

        if (deck.SessionId != request.SessionId)
            throw new ForbiddenException("Deck does not belong to this session.");

        var discardCardIds = deck.DiscardPile.Take(request.Limit).ToList();
        var discardCards = deck.GetCards(discardCardIds);

        return new DiscardPileDto
        {
            DeckId = deck.Id,
            Cards = discardCards.Select(c => new CardDto
            {
                Id = c.Id,
                Name = c.Name,
                ImageUrl = c.ImageUrl,
                Suit = c.Suit,
                Value = c.Value
            }).ToList(),
            TotalCount = deck.DiscardPile.Count
        };
    }
}
