using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;

/// <summary>
/// Handler for adding a private game.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// Implements auto-redirect to SharedGame when BggId exists in catalog.
/// </summary>
internal sealed class AddPrivateGameCommandHandler : ICommandHandler<AddPrivateGameCommand, PrivateGameDto>
{
    private readonly IPrivateGameRepository _privateGameRepository;
    private readonly IUserLibraryRepository _userLibraryRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddPrivateGameCommandHandler> _logger;

    public AddPrivateGameCommandHandler(
        IPrivateGameRepository privateGameRepository,
        IUserLibraryRepository userLibraryRepository,
        ISharedGameRepository sharedGameRepository,
        IUnitOfWork unitOfWork,
        ILogger<AddPrivateGameCommandHandler> logger)
    {
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
        _userLibraryRepository = userLibraryRepository ?? throw new ArgumentNullException(nameof(userLibraryRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PrivateGameDto> Handle(AddPrivateGameCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Parse source enum
        if (!Enum.TryParse<PrivateGameSource>(command.Source, ignoreCase: true, out var source))
        {
            throw new DomainException($"Invalid source: {command.Source}. Must be 'Manual' or 'BoardGameGeek'");
        }

        // Auto-redirect: Check if BggId already exists in shared catalog
        if (command.BggId.HasValue)
        {
            var existingSharedGame = await _sharedGameRepository.GetByBggIdAsync(command.BggId.Value, cancellationToken).ConfigureAwait(false);
            if (existingSharedGame != null)
            {
                _logger.LogInformation(
                    "BggId {BggId} already exists in shared catalog as game {GameId}. " +
                    "User should add from shared catalog instead of creating private game.",
                    command.BggId.Value,
                    existingSharedGame.Id);

                throw new ConflictException(
                    $"Game with BGG ID {command.BggId.Value} already exists in the shared catalog (Game ID: {existingSharedGame.Id}). " +
                    $"Please add it from the catalog instead.");
            }
        }

        // Check for duplicate in user's private games
        if (command.BggId.HasValue)
        {
            if (await _privateGameRepository.ExistsByOwnerAndBggIdAsync(command.UserId, command.BggId.Value, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogWarning(
                    "User {UserId} attempted to add duplicate private game with BggId {BggId}",
                    command.UserId,
                    command.BggId.Value);

                throw new ConflictException($"You already have a private game with BGG ID {command.BggId.Value}");
            }
        }

        // Create private game using appropriate factory method
        PrivateGame privateGame;

        if (source == PrivateGameSource.BoardGameGeek && command.BggId.HasValue)
        {
            privateGame = PrivateGame.CreateFromBgg(
                ownerId: command.UserId,
                bggId: command.BggId.Value,
                title: command.Title,
                yearPublished: command.YearPublished,
                description: command.Description,
                minPlayers: command.MinPlayers,
                maxPlayers: command.MaxPlayers,
                playingTimeMinutes: command.PlayingTimeMinutes,
                minAge: command.MinAge,
                complexityRating: command.ComplexityRating,
                imageUrl: command.ImageUrl,
                thumbnailUrl: command.ThumbnailUrl);

            _logger.LogInformation(
                "Created private game from BGG {BggId} for user {UserId}: {Title}",
                command.BggId.Value,
                command.UserId,
                command.Title);
        }
        else if (source == PrivateGameSource.Manual)
        {
            privateGame = PrivateGame.CreateManual(
                ownerId: command.UserId,
                title: command.Title,
                minPlayers: command.MinPlayers,
                maxPlayers: command.MaxPlayers,
                yearPublished: command.YearPublished,
                description: command.Description,
                playingTimeMinutes: command.PlayingTimeMinutes,
                minAge: command.MinAge,
                complexityRating: command.ComplexityRating,
                imageUrl: command.ImageUrl);

            _logger.LogInformation(
                "Created manual private game for user {UserId}: {Title}",
                command.UserId,
                command.Title);
        }
        else
        {
            throw new DomainException("BoardGameGeek source requires BggId");
        }

        await _privateGameRepository.AddAsync(privateGame, cancellationToken).ConfigureAwait(false);

        // Create UserLibraryEntry to link the private game to user's library
        // Use AddForPrivateGameAsync to correctly set PrivateGameId instead of SharedGameId
        var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), command.UserId, privateGame.Id);
        await _userLibraryRepository.AddForPrivateGameAsync(libraryEntry, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Added private game {PrivateGameId} to library for user {UserId}",
            privateGame.Id,
            command.UserId);

        return MapToDto(privateGame);
    }

    private static PrivateGameDto MapToDto(PrivateGame game)
    {
        return new PrivateGameDto(
            Id: game.Id,
            OwnerId: game.OwnerId,
            Source: game.Source.ToString(),
            BggId: game.BggId,
            Title: game.Title,
            YearPublished: game.YearPublished,
            Description: game.Description,
            MinPlayers: game.MinPlayers,
            MaxPlayers: game.MaxPlayers,
            PlayingTimeMinutes: game.PlayingTimeMinutes,
            MinAge: game.MinAge,
            ComplexityRating: game.ComplexityRating,
            ImageUrl: game.ImageUrl,
            ThumbnailUrl: game.ThumbnailUrl,
            CreatedAt: game.CreatedAt,
            UpdatedAt: game.UpdatedAt,
            BggSyncedAt: game.BggSyncedAt,
            CanProposeToCatalog: game.BggId.HasValue // Can only propose BGG-sourced games
        );
    }
}
