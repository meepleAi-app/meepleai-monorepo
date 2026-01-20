using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for bulk importing games from BoardGameGeek or manual data.
/// Processes imports sequentially to avoid DbContext thread-safety issues.
/// Error isolation ensures individual failures don't stop the entire batch.
/// </summary>
internal sealed class BulkImportGamesCommandHandler : ICommandHandler<BulkImportGamesCommand, BulkImportResultDto>
{
    private readonly IMediator _mediator;
    private readonly ILogger<BulkImportGamesCommandHandler> _logger;

#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback placeholder for manual imports
    private const string DefaultPlaceholderImageUrl = "https://via.placeholder.com/300x300?text=No+Image";
#pragma warning restore S1075

    public BulkImportGamesCommandHandler(
        IMediator mediator,
        ILogger<BulkImportGamesCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BulkImportResultDto> Handle(BulkImportGamesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("Starting bulk import of {Count} games", command.Games.Count);

        var successCount = 0;
        var failureCount = 0;
        var errors = new List<string>();
        var importedGameIds = new List<Guid>();

        // Process games sequentially to avoid DbContext thread-safety issues
        // EF Core DbContext is not thread-safe and cannot be used concurrently
        foreach (var game in command.Games)
        {
            try
            {
                Guid gameId;

                if (game.BggId.HasValue)
                {
                    // Import from BGG using existing handler
                    var importCommand = new ImportGameFromBggCommand(game.BggId.Value, command.UserId);
                    gameId = await _mediator.Send(importCommand, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    // Manual import (create directly)
                    var createCommand = new CreateSharedGameCommand(
                        Title: game.Title!,
                        YearPublished: game.YearPublished ?? DateTime.UtcNow.Year,
                        Description: game.Description ?? "Manually imported",
                        MinPlayers: game.MinPlayers ?? 1,
                        MaxPlayers: game.MaxPlayers ?? 4,
                        PlayingTimeMinutes: game.PlayingTimeMinutes ?? 60,
                        MinAge: game.MinAge ?? 8,
                        ComplexityRating: null,
                        AverageRating: null,
                        ImageUrl: DefaultPlaceholderImageUrl,
                        ThumbnailUrl: DefaultPlaceholderImageUrl,
                        Rules: null,
                        CreatedBy: command.UserId,
                        BggId: null);

                    gameId = await _mediator.Send(createCommand, cancellationToken).ConfigureAwait(false);
                }

                importedGameIds.Add(gameId);
                successCount++;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // HANDLER BOUNDARY: Bulk import should continue even if individual imports fail
            catch (Exception ex)
#pragma warning restore CA1031
            {
                var errorMsg = $"Failed to import game (BggId: {game.BggId}, Title: {game.Title}): {ex.Message}";
                errors.Add(errorMsg);
                failureCount++;

                _logger.LogWarning(ex, "Bulk import failed for game: BggId={BggId}, Title={Title}",
                    game.BggId, game.Title);
            }
        }

        _logger.LogInformation(
            "Bulk import completed: {SuccessCount} succeeded, {FailureCount} failed",
            successCount, failureCount);

        return new BulkImportResultDto(successCount, failureCount, errors, importedGameIds);
    }
}
