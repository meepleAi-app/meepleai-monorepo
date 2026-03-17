using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Infrastructure.Services;
using Api.BoundedContexts.UserLibrary.Application.EventHandlers;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that reads game suggestion events from <see cref="GameSuggestionChannel"/>
/// and processes them. Delegates to <see cref="GamePreAddedHandler"/> for PreAdded suggestions
/// and <see cref="GameSuggestedHandler"/> for Suggested suggestions.
/// Retries up to 3 times with exponential backoff on failure.
/// </summary>
internal sealed class GameSuggestionProcessorService : BackgroundService
{
    internal const int MaxRetries = 3;

    private readonly GameSuggestionChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GameSuggestionProcessorService> _logger;

    public GameSuggestionProcessorService(
        GameSuggestionChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<GameSuggestionProcessorService> logger)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GameSuggestionProcessorService started");

        await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
#pragma warning disable CA1031 // Do not catch general exception types
            // BACKGROUND SERVICE: Generic catch prevents service from crashing the host process.
            try
            {
                await ProcessEventWithRetryAsync(evt, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(
                    ex,
                    "Failed to process game suggestion event for user {UserId} after all retries",
                    evt.UserId);
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation("GameSuggestionProcessorService stopped");
    }

    private async Task ProcessEventWithRetryAsync(GameSuggestionEvent evt, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await ProcessEventAsync(evt, ct).ConfigureAwait(false);
                return; // Success
            }
            catch (OperationCanceledException)
            {
                throw; // Let cancellation propagate
            }
#pragma warning disable CA1031
            catch (Exception ex) when (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // 1s, 2s, 4s
                _logger.LogWarning(
                    ex,
                    "Game suggestion processing attempt {Attempt}/{MaxRetries} failed for user {UserId}. Retrying in {Delay}s",
                    attempt, MaxRetries, evt.UserId, delay.TotalSeconds);

                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
#pragma warning restore CA1031
        }

        // Final attempt — let exception propagate to outer handler
        await ProcessEventAsync(evt, ct).ConfigureAwait(false);
    }

    internal async Task ProcessEventAsync(GameSuggestionEvent evt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var preAddedHandler = scope.ServiceProvider.GetRequiredService<GamePreAddedHandler>();
        var suggestedHandler = scope.ServiceProvider.GetRequiredService<GameSuggestedHandler>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        _logger.LogInformation(
            "Processing {Count} game suggestion(s) for user {UserId}",
            evt.Suggestions.Count, evt.UserId);

        foreach (var suggestion in evt.Suggestions)
        {
            switch (suggestion.Type)
            {
                case GameSuggestionType.PreAdded:
                    await preAddedHandler.HandleAsync(
                        evt.UserId, suggestion.GameId, evt.InvitedByUserId, ct)
                        .ConfigureAwait(false);
                    break;

                case GameSuggestionType.Suggested:
                    await suggestedHandler.HandleAsync(
                        evt.UserId, suggestion.GameId, evt.InvitedByUserId, ct)
                        .ConfigureAwait(false);
                    break;

                default:
                    _logger.LogWarning(
                        "Unknown game suggestion type {Type} for game {GameId}",
                        suggestion.Type, suggestion.GameId);
                    break;
            }
        }

        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Successfully processed game suggestions for user {UserId}",
            evt.UserId);
    }
}
