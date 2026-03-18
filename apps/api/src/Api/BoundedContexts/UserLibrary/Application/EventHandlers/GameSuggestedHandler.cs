using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;

namespace Api.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// Handles Suggested game suggestions from the invitation flow.
/// Creates a GameSuggestion entity so the user can view and optionally accept the suggestion.
/// Idempotent: skips if a suggestion already exists for the user+game combination.
/// </summary>
internal sealed class GameSuggestedHandler
{
    private readonly IGameSuggestionRepository _gameSuggestionRepo;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameSuggestedHandler> _logger;

    public GameSuggestedHandler(
        IGameSuggestionRepository gameSuggestionRepo,
        TimeProvider timeProvider,
        ILogger<GameSuggestedHandler> logger)
    {
        _gameSuggestionRepo = gameSuggestionRepo ?? throw new ArgumentNullException(nameof(gameSuggestionRepo));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a game suggestion for the user if one doesn't already exist.
    /// </summary>
    /// <param name="userId">The user to create the suggestion for</param>
    /// <param name="gameId">The game to suggest</param>
    /// <param name="suggestedByUserId">The admin who suggested the game</param>
    /// <param name="ct">Cancellation token</param>
    public async Task HandleAsync(Guid userId, Guid gameId, Guid suggestedByUserId, CancellationToken ct)
    {
        // Idempotent: skip if suggestion already exists
        if (await _gameSuggestionRepo.ExistsForUserAndGameAsync(userId, gameId, ct).ConfigureAwait(false))
        {
            _logger.LogDebug(
                "GameSuggestion already exists for user {UserId} and game {GameId}. Skipping",
                userId, gameId);
            return;
        }

        var suggestion = GameSuggestion.Create(
            userId, gameId, suggestedByUserId, "invitation", _timeProvider);
        await _gameSuggestionRepo.AddAsync(suggestion, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Created game suggestion for user {UserId}, game {GameId}",
            userId, gameId);
    }
}
