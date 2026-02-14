using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for EnqueueBggBatchFromJsonCommand
/// Parses JSON, checks duplicates, enqueues new games with best-effort strategy
/// Issue #4352: Backend - Bulk Import JSON Command
/// </summary>
internal class EnqueueBggBatchFromJsonCommandHandler
    : IRequestHandler<EnqueueBggBatchFromJsonCommand, BulkImportResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IBggImportQueueService _queueService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<EnqueueBggBatchFromJsonCommandHandler> _logger;

    public EnqueueBggBatchFromJsonCommandHandler(
        IBggImportQueueService queueService,
        MeepleAiDbContext dbContext,
        ILogger<EnqueueBggBatchFromJsonCommandHandler> logger)
    {
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BulkImportResult> Handle(
        EnqueueBggBatchFromJsonCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting bulk import from JSON for user {UserId}",
            request.UserId);

        // 1. Parse JSON to list of games
        List<BggGameJsonDto> parsedGames;
        try
        {
            parsedGames = JsonSerializer.Deserialize<List<BggGameJsonDto>>(
                request.JsonContent,
                JsonOptions)
                ?? new List<BggGameJsonDto>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON content");
            return new BulkImportResult
            {
                Total = 0,
                Enqueued = 0,
                Skipped = 0,
                Failed = 1,
                Errors = new List<BulkImportError>
                {
                    new BulkImportError
                    {
                        BggId = null,
                        GameName = null,
                        Reason = $"JSON parsing failed: {ex.Message}",
                        ErrorType = "InvalidJson"
                    }
                }
            };
        }

        var result = new BulkImportResult
        {
            Total = parsedGames.Count,
            Enqueued = 0,
            Skipped = 0,
            Failed = 0,
            Errors = new List<BulkImportError>()
        };

        if (parsedGames.Count == 0)
        {
            _logger.LogWarning("Empty JSON array provided");
            return result;
        }

        // 2. Query existing BGG IDs in a single batch (efficient)
        var bggIds = parsedGames.Select(g => g.BggId).Distinct().ToList();
        var existingBggIds = await _dbContext.SharedGames
            .Where(g => bggIds.Contains(g.BggId!.Value) && g.BggId.HasValue)
            .Select(g => g.BggId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingSet = new HashSet<int>(existingBggIds);

        _logger.LogInformation(
            "Found {ExistingCount} duplicates out of {TotalCount} games",
            existingSet.Count,
            bggIds.Count);

        // 3. Best-effort processing: skip duplicates, continue on errors
        foreach (var game in parsedGames)
        {
            try
            {
                // Validation: BGG ID must be positive
                if (game.BggId <= 0)
                {
                    result.Failed++;
                    result.Errors.Add(new BulkImportError
                    {
                        BggId = game.BggId,
                        GameName = game.Name,
                        Reason = "BGG ID must be positive",
                        ErrorType = "ValidationError"
                    });
                    continue;
                }

                // Check duplicate
                if (existingSet.Contains(game.BggId))
                {
                    result.Skipped++;
                    result.Errors.Add(new BulkImportError
                    {
                        BggId = game.BggId,
                        GameName = game.Name,
                        Reason = "Game already exists in catalog",
                        ErrorType = "Duplicate"
                    });
                    continue;
                }

                // Enqueue new game
                await _queueService
                    .EnqueueAsync(game.BggId, game.Name, cancellationToken)
                    .ConfigureAwait(false);

                result.Enqueued++;

                _logger.LogDebug(
                    "Enqueued game: {BggId} - {Name}",
                    game.BggId,
                    game.Name);
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add(new BulkImportError
                {
                    BggId = game.BggId,
                    GameName = game.Name,
                    Reason = $"Failed to enqueue: {ex.Message}",
                    ErrorType = "ApiError"
                });

                _logger.LogError(
                    ex,
                    "Failed to enqueue game {BggId} - {Name}",
                    game.BggId,
                    game.Name);

                // Continue processing next game (best-effort)
            }
        }

        _logger.LogInformation(
            "Bulk import completed: Total={Total}, Enqueued={Enqueued}, Skipped={Skipped}, Failed={Failed}",
            result.Total,
            result.Enqueued,
            result.Skipped,
            result.Failed);

        return result;
    }
}

/// <summary>
/// Internal DTO for JSON parsing
/// </summary>
internal record BggGameJsonDto
{
    public int BggId { get; init; }
    public string Name { get; init; } = string.Empty;
}
