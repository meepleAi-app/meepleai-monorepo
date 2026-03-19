using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles game creation command.
/// Issue #3372: Added PDF linking support during game creation.
/// </summary>
internal class CreateGameCommandHandler : ICommandHandler<CreateGameCommand, GameDto>
{
    private readonly IGameRepository _gameRepository;
    private readonly IPdfDocumentRepository _pdfDocumentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateGameCommandHandler(
        IGameRepository gameRepository,
        IPdfDocumentRepository pdfDocumentRepository,
        IUnitOfWork unitOfWork)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _pdfDocumentRepository = pdfDocumentRepository ?? throw new ArgumentNullException(nameof(pdfDocumentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameDto> Handle(CreateGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Create value objects
        var title = new GameTitle(command.Title);
        var publisher = command.Publisher != null ? new Publisher(command.Publisher) : null;
        var yearPublished = command.YearPublished.HasValue ? new YearPublished(command.YearPublished.Value) : null;

        PlayerCount? playerCount = null;
        if (command.MinPlayers.HasValue && command.MaxPlayers.HasValue)
        {
            playerCount = new PlayerCount(command.MinPlayers.Value, command.MaxPlayers.Value);
        }

        PlayTime? playTime = null;
        if (command.MinPlayTimeMinutes.HasValue && command.MaxPlayTimeMinutes.HasValue)
        {
            playTime = new PlayTime(command.MinPlayTimeMinutes.Value, command.MaxPlayTimeMinutes.Value);
        }

        // Create Game aggregate
        var game = new Game(
            id: Guid.NewGuid(),
            title: title,
            publisher: publisher,
            yearPublished: yearPublished,
            playerCount: playerCount,
            playTime: playTime
        );

        // Set images if provided
        if (!string.IsNullOrWhiteSpace(command.IconUrl) || !string.IsNullOrWhiteSpace(command.ImageUrl))
        {
            game.SetImages(command.IconUrl, command.ImageUrl);
        }

        // Link to BGG if ID provided
        if (command.BggId.HasValue)
        {
            game.LinkToBgg(command.BggId.Value);
        }

        // Link to SharedGameCatalog if ID provided (Issue #2373)
        if (command.SharedGameId.HasValue)
        {
            game.LinkToSharedGame(command.SharedGameId.Value);
        }

        // Persist game first
        await _gameRepository.AddAsync(game, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Link PDF to game if PdfId provided (Issue #3372)
        if (command.PdfId.HasValue)
        {
            var pdfDocument = await _pdfDocumentRepository.GetByIdAsync(command.PdfId.Value, cancellationToken).ConfigureAwait(false)
                ?? throw new NotFoundException("PdfDocument", command.PdfId.Value.ToString());

            pdfDocument.LinkToGame(game.Id);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // Map to DTO
        return MapToDto(game);
    }

    private static GameDto MapToDto(Game game)
    {
        return new GameDto(
            Id: game.Id,
            Title: game.Title.Value,
            Publisher: game.Publisher?.Name,
            YearPublished: game.YearPublished?.Value,
            MinPlayers: game.PlayerCount?.Min,
            MaxPlayers: game.PlayerCount?.Max,
            MinPlayTimeMinutes: game.PlayTime?.MinMinutes,
            MaxPlayTimeMinutes: game.PlayTime?.MaxMinutes,
            BggId: game.BggId,
            CreatedAt: game.CreatedAt,
            IconUrl: game.IconUrl,
            ImageUrl: game.ImageUrl,
            SharedGameId: game.SharedGameId
        );
    }
}
