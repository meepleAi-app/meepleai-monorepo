using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Services;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure.ExternalServices.BoardGameGeek;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
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

        // 1. The tier's limit of private games. #5020: the same rule as manual creation — the private games the user
        // has, counted under the owner's lock with the insert (step 6), and 402 over it. It was a daily Redis counter
        // of creations, which a deletion never gave back, answering 400.
        var limits = await _tierEnforcementService
            .GetLimitsAsync(command.UserId, cancellationToken)
            .ConfigureAwait(false);

        // A user already at the limit is refused before the BGG call: no external call for an import that cannot be
        // saved. This read is not the guard — the count under the lock, at the insert, is.
        if (await _privateGameRepository.CountByOwnerIdAsync(command.UserId, cancellationToken).ConfigureAwait(false)
            >= limits.MaxPrivateGames)
        {
            throw new PrivateGameQuotaExceededException(limits.MaxPrivateGames);
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

        // 4-6. Create the PrivateGame and its library entry, within the quota, atomically. #5020: built inside the
        // staging, once per transaction attempt: a replayed attempt must not reuse the rows of one that did not commit.
        PrivateGame? privateGame = null;
        UserLibraryEntry? libraryEntry = null;
        await PrivateGameQuota.CreateWithinQuotaAsync(
            command.UserId,
            limits.MaxPrivateGames,
            async ct =>
            {
                privateGame = PrivateGame.CreateFromBgg(
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
                    // #5066, BGG freeze #2123 / ADR-059 §5: BGG's <image>/<thumbnail> point at its CDN, which the
                    // browser must never be asked for. Same as manual creation: the cover materializes later via
                    // the PDF flow.
                    imageUrl: null,
                    thumbnailUrl: null);

                libraryEntry = new UserLibraryEntry(
                    id: Guid.NewGuid(),
                    userId: command.UserId,
                    gameId: privateGame.Id);

                await _privateGameRepository.AddAsync(privateGame, ct).ConfigureAwait(false);
                await _userLibraryRepository.AddForPrivateGameAsync(libraryEntry, ct).ConfigureAwait(false);
            },
            _privateGameRepository,
            _unitOfWork,
            _logger,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} imported BGG game {BggId} as PrivateGame {PrivateGameId} with LibraryEntry {LibraryEntryId}",
            command.UserId, command.BggId, privateGame!.Id, libraryEntry!.Id);

        return new ImportBggGameResult(
            PrivateGameId: privateGame.Id,
            LibraryEntryId: libraryEntry.Id,
            Title: privateGame.Title,
            ImageUrl: privateGame.ImageUrl);
    }
}
