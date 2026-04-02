using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job that computes and persists a KB coverage score for every shared game.
/// KB-05: Runs daily at 02:00 UTC to reflect how well each game's knowledge base is populated.
///
/// The score (0–100) considers:
///   - Rulebook presence (+50), FAQ/Reference (+15), Errata (+5)
///   - Chunk density (saturates at 200 completed chunks → +20)
///   - Freshness: indexed within the last 90 days (+10)
///
/// Result stored in SystemConfiguration as "KB:Coverage:{gameId}" (JSON: {"score":75,"level":"Complete"}).
/// </summary>
[DisallowConcurrentExecution]
internal sealed class KbCoverageComputeJob : IJob
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IConfigurationRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<KbCoverageComputeJob> _logger;

    // Sentinel user ID used for system-generated configuration entries.
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public KbCoverageComputeJob(
        MeepleAiDbContext dbContext,
        IConfigurationRepository configRepository,
        IUnitOfWork unitOfWork,
        ILogger<KbCoverageComputeJob> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        _logger.LogInformation(
            "KbCoverageComputeJob starting: FireTime={FireTime}",
            context.FireTimeUtc);

        try
        {
            // Load all VectorDocuments that have a SharedGameId, joined to PdfDocuments for category.
            var rows = await _dbContext.VectorDocuments
                .Where(v => v.SharedGameId != null)
                .Join(
                    _dbContext.PdfDocuments,
                    v => v.PdfDocumentId,
                    p => p.Id,
                    (v, p) => new
                    {
                        SharedGameId = v.SharedGameId!.Value,
                        v.ChunkCount,
                        v.IndexingStatus,
                        v.IndexedAt,
                        p.DocumentCategory
                    })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            // Group by game.
            var byGame = rows.GroupBy(r => r.SharedGameId);

            int processed = 0;
            int errors = 0;

            foreach (var group in byGame)
            {
                var gameId = group.Key;
                var items = group.ToList();

                // Parse DocumentCategory strings to enum values.
                var parsed = items.Select(x => new
                {
                    Category = Enum.TryParse<DocumentCategory>(x.DocumentCategory, out var cat)
                        ? cat
                        : DocumentCategory.Other,
                    x.IndexingStatus,
                    x.ChunkCount,
                    x.IndexedAt
                }).ToList();

                bool hasRulebook = parsed.Any(x =>
                    x.Category == DocumentCategory.Rulebook &&
                    string.Equals(x.IndexingStatus, "completed", StringComparison.Ordinal));

                bool hasErrata = parsed.Any(x =>
                    x.Category == DocumentCategory.Errata &&
                    string.Equals(x.IndexingStatus, "completed", StringComparison.Ordinal));

                bool hasFaq = parsed.Any(x =>
                    (x.Category == DocumentCategory.QuickStart ||
                     x.Category == DocumentCategory.Reference) &&
                    string.Equals(x.IndexingStatus, "completed", StringComparison.Ordinal));

                int completedChunks = parsed
                    .Where(x => string.Equals(x.IndexingStatus, "completed", StringComparison.Ordinal))
                    .Sum(x => x.ChunkCount);

                int totalChunks = parsed.Sum(x => x.ChunkCount);

                DateTime? lastIndexed = parsed
                    .Where(x => x.IndexedAt.HasValue)
                    .Select(x => x.IndexedAt!.Value)
                    .DefaultIfEmpty()
                    .Max();

                int daysSince = lastIndexed.HasValue
                    ? (int)(DateTime.UtcNow - lastIndexed.Value).TotalDays
                    : int.MaxValue;

                int score = ComputeScore(hasRulebook, hasErrata, hasFaq, completedChunks, totalChunks, daysSince);
                string level = ScoreToLevel(score);

                string configKey = $"KB:Coverage:{gameId}";
                string configValue = JsonSerializer.Serialize(new { score, level });

                try
                {
                    var existing = await _configRepository
                        .GetByKeyAsync(configKey, cancellationToken: ct)
                        .ConfigureAwait(false);

                    if (existing is null)
                    {
                        var newConfig = new Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration(
                            id: Guid.NewGuid(),
                            key: new ConfigKey(configKey),
                            value: configValue,
                            valueType: "json",
                            createdByUserId: SystemUserId,
                            description: $"KB coverage score for game {gameId}",
                            category: "KnowledgeBase");

                        await _configRepository.AddAsync(newConfig, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        existing.UpdateValue(configValue, SystemUserId);
                        await _configRepository.UpdateAsync(existing, ct).ConfigureAwait(false);
                    }

                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "KbCoverageComputeJob: failed to persist coverage for game {GameId}",
                        gameId);
                    errors++;
                }
            }

            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "KbCoverageComputeJob completed: Processed={Processed}, Errors={Errors}",
                processed, errors);

            context.Result = new { Success = true, Processed = processed, Errors = errors };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
        // Background tasks must not throw exceptions (would terminate task scheduler).
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "KbCoverageComputeJob failed");
            context.Result = new { Success = false, Error = ex.Message };
        }
#pragma warning restore CA1031
    }

    // ── Static helpers (internal for unit testability) ────────────────────────

    /// <summary>
    /// Computes the KB coverage score (0–100) from individual signal values.
    /// </summary>
    internal static int ComputeScore(
        bool hasRulebook,
        bool hasErrata,
        bool hasFaq,
        int completedChunks,
        int totalChunks,
        int daysSinceLastIndex)
    {
        int score = 0;

        if (hasRulebook) score += 50;
        if (hasFaq)      score += 15;
        if (hasErrata)   score += 5;

        // Chunk density: saturates at 200 completed chunks
        if (totalChunks > 0)
            score += (int)Math.Round(Math.Min(completedChunks / 200.0, 1.0) * 20);

        // Freshness bonus: indexed within the last 90 days
        if (daysSinceLastIndex <= 90) score += 10;

        return Math.Min(score, 100);
    }

    /// <summary>
    /// Maps a numeric score to a human-readable coverage level label.
    /// </summary>
    internal static string ScoreToLevel(int score) => score switch
    {
        >= 75 => "Complete",
        >= 50 => "Standard",
        >= 25 => "Basic",
        _     => "None"
    };
}
