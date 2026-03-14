#pragma warning disable MA0048 // File name must match type name - Contains related handlers
using Api.BoundedContexts.GameToolbox.Adapters;
using Api.BoundedContexts.GameToolbox.Application.Commands;
using Api.BoundedContexts.GameToolbox.Application.DTOs;
using Api.BoundedContexts.GameToolbox.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.GameToolbox.Application.Handlers;

internal class CreateCardDeckCommandHandler : ICommandHandler<CreateCardDeckCommand, ToolboxToolDto>
{
    private readonly IToolboxRepository _toolboxRepository;
    private readonly CardDeckAdapter _cardDeckAdapter;

    public CreateCardDeckCommandHandler(IToolboxRepository toolboxRepository, CardDeckAdapter cardDeckAdapter)
    {
        _toolboxRepository = toolboxRepository ?? throw new ArgumentNullException(nameof(toolboxRepository));
        _cardDeckAdapter = cardDeckAdapter ?? throw new ArgumentNullException(nameof(cardDeckAdapter));
    }

    public async Task<ToolboxToolDto> Handle(CreateCardDeckCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var toolbox = await _toolboxRepository.GetByIdAsync(command.ToolboxId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("Toolbox", command.ToolboxId.ToString());

        // Create the SessionDeck via adapter
        var includeJokers = string.Equals(command.DeckType, "Standard52WithJokers", StringComparison.Ordinal);
        var isCustom = string.Equals(command.DeckType, "Custom", StringComparison.Ordinal);

        SessionDeck deck;
        if (isCustom && command.CustomCards is { Count: > 0 })
        {
            var cards = command.CustomCards.Select((c, i) =>
                Card.Create(c.Name, suit: c.Suit, value: c.Value, sortOrder: i)).ToList();
            deck = await _cardDeckAdapter.CreateCustomDeckAsync(
                toolbox.Id, command.Name, cards, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            deck = await _cardDeckAdapter.CreateStandardDeckAsync(
                toolbox.Id, command.Name, includeJokers, cancellationToken).ConfigureAwait(false);
        }

        // Add reference tool to the Toolbox
        var config = System.Text.Json.JsonSerializer.Serialize(new
        {
            deckId = deck.Id,
            deckType = command.DeckType,
            name = command.Name
        });
        var tool = toolbox.AddTool("CardDeck", config);

        await _toolboxRepository.UpdateAsync(toolbox, cancellationToken).ConfigureAwait(false);
        await _toolboxRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToolboxMapper.ToDto(tool);
    }
}

internal class ShuffleCardDeckCommandHandler : ICommandHandler<ShuffleCardDeckCommand, Unit>
{
    private readonly CardDeckAdapter _adapter;

    public ShuffleCardDeckCommandHandler(CardDeckAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<Unit> Handle(ShuffleCardDeckCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _adapter.ShuffleAsync(command.DeckId, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

internal class DrawCardsCommandHandler : ICommandHandler<DrawCardsCommand, CardDrawResultDto>
{
    private readonly CardDeckAdapter _adapter;

    public DrawCardsCommandHandler(CardDeckAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<CardDrawResultDto> Handle(DrawCardsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var drawnCards = await _adapter.DrawAsync(command.DeckId, command.Count, cancellationToken).ConfigureAwait(false);
        var remaining = await _adapter.GetRemainingCountAsync(command.DeckId, cancellationToken).ConfigureAwait(false);

        var cardDtos = drawnCards.Select(c => new DrawnCardDto(
            Id: c.Id,
            Name: c.Name,
            Value: c.Value,
            Suit: c.Suit,
            CustomProperties: new Dictionary<string, string>(StringComparer.Ordinal)
        )).ToList();

        return new CardDrawResultDto(cardDtos, remaining);
    }
}

internal class ResetCardDeckCommandHandler : ICommandHandler<ResetCardDeckCommand, Unit>
{
    private readonly CardDeckAdapter _adapter;

    public ResetCardDeckCommandHandler(CardDeckAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    }

    public async Task<Unit> Handle(ResetCardDeckCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _adapter.ResetAsync(command.DeckId, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
