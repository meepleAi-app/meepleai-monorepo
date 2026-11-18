using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Services;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.SharedKernel.Services;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

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
    private readonly ITierEnforcementService _tierEnforcementService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddPrivateGameCommandHandler> _logger;

    public AddPrivateGameCommandHandler(
        IPrivateGameRepository privateGameRepository,
        IUserLibraryRepository userLibraryRepository,
        ISharedGameRepository sharedGameRepository,
        ITierEnforcementService tierEnforcementService,
        IUnitOfWork unitOfWork,
        ILogger<AddPrivateGameCommandHandler> logger)
    {
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
        _userLibraryRepository = userLibraryRepository ?? throw new ArgumentNullException(nameof(userLibraryRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _tierEnforcementService = tierEnforcementService ?? throw new ArgumentNullException(nameof(tierEnforcementService));
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

        // #4978: the tier's MaxPrivateGames counts the private games the user has (deleted ones excluded by the
        // query filter). This path never checked it, so a free user (3) created private games without end. #5020: the
        // count and the insert are one atomic step, below (PrivateGameQuota).
        var limits = await _tierEnforcementService.GetLimitsAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        if (source == PrivateGameSource.BoardGameGeek && !command.BggId.HasValue)
        {
            throw new DomainException("BoardGameGeek source requires BggId");
        }

        // #5020: the aggregates are built inside the staging, once per transaction attempt: a replayed attempt must not
        // reuse the rows of one that did not commit (PrivateGameQuota).
        PrivateGame? privateGame = null;
        await PrivateGameQuota.CreateWithinQuotaAsync(
            command.UserId,
            limits.MaxPrivateGames,
            async ct =>
            {
                privateGame = BuildPrivateGame(command, source);

                // Link the private game to the user's library. AddForPrivateGameAsync sets PrivateGameId, not SharedGameId.
                var libraryEntry = new UserLibraryEntry(Guid.NewGuid(), command.UserId, privateGame.Id);

                await _privateGameRepository.AddAsync(privateGame, ct).ConfigureAwait(false);
                await _userLibraryRepository.AddForPrivateGameAsync(libraryEntry, ct).ConfigureAwait(false);
            },
            _privateGameRepository,
            _unitOfWork,
            _logger,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Added {Source} private game {PrivateGameId} to library for user {UserId}: {Title}",
            source,
            privateGame!.Id,
            command.UserId,
            command.Title);

        return MapToDto(privateGame);
    }

    /// <summary>The private game the command describes, from BGG data or entered by hand.</summary>
    private static PrivateGame BuildPrivateGame(AddPrivateGameCommand command, PrivateGameSource source)
    {
        if (source == PrivateGameSource.BoardGameGeek && command.BggId.HasValue)
        {
            return PrivateGame.CreateFromBgg(
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
                imageUrl: null, // BGG freeze #2123 / ADR-059: no user-supplied external URLs; cover materializes later via PDF flow
                thumbnailUrl: null);
        }

        return PrivateGame.CreateManual(
            ownerId: command.UserId,
            title: command.Title,
            minPlayers: command.MinPlayers,
            maxPlayers: command.MaxPlayers,
            yearPublished: command.YearPublished,
            description: command.Description,
            playingTimeMinutes: command.PlayingTimeMinutes,
            minAge: command.MinAge,
            complexityRating: command.ComplexityRating,
            imageUrl: null); // BGG freeze #2123 / ADR-059: no user-supplied external URLs; cover materializes later via PDF flow
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
