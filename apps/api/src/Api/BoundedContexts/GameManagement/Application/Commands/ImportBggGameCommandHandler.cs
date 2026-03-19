using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure.ExternalServices.BoardGameGeek;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.SharedKernel.Services;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Handles importing a BoardGameGeek game as a PrivateGame with tier quota enforcement.
/// Game Night Improvvisata - E1-2: Import BGG game with tier enforcement.
/// </summary>
internal sealed class ImportBggGameCommandHandler
    : ICommandHandler<ImportBggGameCommand, ImportBggGameResult>
{
    private readonly ITierEnforcementService _tierEnforcementService;
    private readonly IBggApiClient _bggApiClient;
    private readonly IPrivateGameRepository _privateGameRepository;
    private readonly IUserLibraryRepository _userLibraryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ImportBggGameCommandHandler> _logger;

    public ImportBggGameCommandHandler(
        ITierEnforcementService tierEnforcementService,
        IBggApiClient bggApiClient,
        IPrivateGameRepository privateGameRepository,
        IUserLibraryRepository userLibraryRepository,
        IUnitOfWork unitOfWork,
        ILogger<ImportBggGameCommandHandler> logger)
    {
        _tierEnforcementService = tierEnforcementService ?? throw new ArgumentNullException(nameof(tierEnforcementService));
        _bggApiClient = bggApiClient ?? throw new ArgumentNullException(nameof(bggApiClient));
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
        _userLibraryRepository = userLibraryRepository ?? throw new ArgumentNullException(nameof(userLibraryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImportBggGameResult> Handle(
        ImportBggGameCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Check tier quota
        var canImport = await _tierEnforcementService
            .CanPerformAsync(command.UserId, TierAction.CreatePrivateGame, cancellationToken)
            .ConfigureAwait(false);

        if (!canImport)
        {
            throw new QuotaExceededException(
                "PrivateGameQuota",
                "You have reached the maximum number of private games allowed on your current plan.");
        }

        // 2. Check for duplicate import
        var alreadyExists = await _privateGameRepository
            .ExistsByOwnerAndBggIdAsync(command.UserId, command.BggId, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyExists)
        {
            throw new ConflictException(
                $"BGG game {command.BggId} has already been imported to your library.");
        }

        // 3. Fetch BGG game details
        var bggDetails = await _bggApiClient
            .GetGameDetailsAsync(command.BggId, cancellationToken)
            .ConfigureAwait(false);

        if (bggDetails is null)
        {
            throw new NotFoundException("BggGame", command.BggId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // 4. Create PrivateGame from BGG data
        var privateGame = PrivateGame.CreateFromBgg(
            ownerId: command.UserId,
            bggId: bggDetails.BggId,
            title: bggDetails.Title,
            yearPublished: bggDetails.YearPublished,
            description: bggDetails.Description,
            minPlayers: bggDetails.MinPlayers ?? 1,
            maxPlayers: bggDetails.MaxPlayers ?? 4,
            playingTimeMinutes: bggDetails.PlayingTimeMinutes,
            minAge: null,
            complexityRating: bggDetails.ComplexityRating,
            imageUrl: bggDetails.ImageUrl,
            thumbnailUrl: bggDetails.ThumbnailUrl);

        // 5. Create UserLibraryEntry for the private game
        var libraryEntry = new UserLibraryEntry(
            id: Guid.NewGuid(),
            userId: command.UserId,
            gameId: privateGame.Id);

        // 6. Persist both entities
        await _privateGameRepository
            .AddAsync(privateGame, cancellationToken)
            .ConfigureAwait(false);

        await _userLibraryRepository
            .AddForPrivateGameAsync(libraryEntry, cancellationToken)
            .ConfigureAwait(false);

        // 7. Save all changes atomically
        await _unitOfWork
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        // 8. Record usage after successful persistence
        await _tierEnforcementService
            .RecordUsageAsync(command.UserId, TierAction.CreatePrivateGame, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} imported BGG game {BggId} as PrivateGame {PrivateGameId} with LibraryEntry {LibraryEntryId}",
            command.UserId, command.BggId, privateGame.Id, libraryEntry.Id);

        return new ImportBggGameResult(
            PrivateGameId: privateGame.Id,
            LibraryEntryId: libraryEntry.Id,
            Title: privateGame.Title,
            ImageUrl: privateGame.ImageUrl);
    }
}
