using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;

namespace Api.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// Handles PreAdded game suggestions from the invitation flow.
/// When an admin pre-adds a game to an invitee's account, this handler
/// adds the game to the user's library and creates a tracking GameSuggestion record.
/// Idempotent: skips if game already in library or suggestion already exists.
/// </summary>
internal sealed class GamePreAddedHandler
{
    private readonly ISharedGameRepository _sharedGameRepo;
    private readonly IUserLibraryRepository _libraryRepo;
    private readonly IGameSuggestionRepository _gameSuggestionRepo;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GamePreAddedHandler> _logger;

    public GamePreAddedHandler(
        ISharedGameRepository sharedGameRepo,
        IUserLibraryRepository libraryRepo,
        IGameSuggestionRepository gameSuggestionRepo,
        TimeProvider timeProvider,
        ILogger<GamePreAddedHandler> logger)
    {
        _sharedGameRepo = sharedGameRepo ?? throw new ArgumentNullException(nameof(sharedGameRepo));
        _libraryRepo = libraryRepo ?? throw new ArgumentNullException(nameof(libraryRepo));
        _gameSuggestionRepo = gameSuggestionRepo ?? throw new ArgumentNullException(nameof(gameSuggestionRepo));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a PreAdded game suggestion: validates the game exists, checks idempotency,
    /// adds to library, and creates a tracking suggestion record marked as accepted.
    /// </summary>
    /// <param name="userId">The user to add the game for</param>
    /// <param name="gameId">The game ID from the shared catalog</param>
    /// <param name="suggestedByUserId">The admin who suggested the game</param>
    /// <param name="ct">Cancellation token</param>
    public async Task HandleAsync(Guid userId, Guid gameId, Guid suggestedByUserId, CancellationToken ct)
    {
        // Validate game exists in catalog
        var game = await _sharedGameRepo.GetByIdAsync(gameId, ct).ConfigureAwait(false);
        if (game is null)
        {
            _logger.LogWarning(
                "PreAdded game {GameId} not found in catalog. Skipping for user {UserId}",
                gameId, userId);
            return;
        }

        // Idempotent: skip if already in library
        if (await _libraryRepo.IsGameInLibraryAsync(userId, gameId, ct).ConfigureAwait(false))
        {
            _logger.LogDebug(
                "Game {GameId} already in library for user {UserId}. Skipping pre-add",
                gameId, userId);
            return;
        }

        // Add to library
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, gameId);
        await _libraryRepo.AddAsync(entry, ct).ConfigureAwait(false);

        // Also create a GameSuggestion record for tracking
        if (!await _gameSuggestionRepo.ExistsForUserAndGameAsync(userId, gameId, ct).ConfigureAwait(false))
        {
            var gameSuggestion = GameSuggestion.Create(
                userId, gameId, suggestedByUserId, "invitation_pre_added", _timeProvider);
            gameSuggestion.Accept(); // Pre-added = auto-accepted
            await _gameSuggestionRepo.AddAsync(gameSuggestion, ct).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Pre-added game {GameId} to library for user {UserId}",
            gameId, userId);
    }
}
