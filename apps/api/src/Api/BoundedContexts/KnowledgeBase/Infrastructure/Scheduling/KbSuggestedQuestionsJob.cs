using System.Text.Json;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job that pre-calculates and persists suggested questions for every shared game
/// that has at least one completed vector document.
/// KB-09: Runs daily at 03:30 UTC.
///
/// For each game, 5 Italian template questions are generated and stored in SystemConfiguration
/// as a JSON string array at key "KB:SuggestedQuestions:{gameId}".
/// </summary>
[DisallowConcurrentExecution]
internal sealed class KbSuggestedQuestionsJob : IJob
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IConfigurationRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<KbSuggestedQuestionsJob> _logger;

    // Sentinel user ID used for system-generated configuration entries.
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private const string CompletedStatus = "completed";

    public KbSuggestedQuestionsJob(
        MeepleAiDbContext dbContext,
        IConfigurationRepository configRepository,
        IUnitOfWork unitOfWork,
        ILogger<KbSuggestedQuestionsJob> logger)
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
            "KbSuggestedQuestionsJob starting: FireTime={FireTime}",
            context.FireTimeUtc);

        try
        {
            // Load distinct SharedGameId values from completed VectorDocuments.
            var gameIds = await _dbContext.VectorDocuments
                .Where(v => v.SharedGameId != null &&
                            v.IndexingStatus == CompletedStatus)
                .Select(v => v.SharedGameId!.Value)
                .Distinct()
                .ToListAsync(ct)
                .ConfigureAwait(false);

            // Load game titles in a single query.
            var gameTitles = await _dbContext.SharedGames
                .Where(g => gameIds.Contains(g.Id))
                .Select(g => new { g.Id, g.Title })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var titleByGameId = gameTitles.ToDictionary(g => g.Id, g => g.Title);

            int processed = 0;
            int errors = 0;

            foreach (var gameId in gameIds)
            {
                titleByGameId.TryGetValue(gameId, out var title);

                var questions = GenerateTemplateQuestions(title);
                string configKey = $"KB:SuggestedQuestions:{gameId}";
                string configValue = JsonSerializer.Serialize(questions);

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
                            description: $"Suggested questions for game {gameId}",
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
                        "KbSuggestedQuestionsJob: failed to persist suggested questions for game {GameId}",
                        gameId);
                    errors++;
                }
            }

            await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

            _logger.LogInformation(
                "KbSuggestedQuestionsJob completed: Processed={Processed}, Errors={Errors}",
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
            _logger.LogError(ex, "KbSuggestedQuestionsJob failed");
            context.Result = new { Success = false, Error = ex.Message };
        }
#pragma warning restore CA1031
    }

    // ── Static helpers (internal for unit testability) ────────────────────────

    /// <summary>
    /// Generates 5 Italian template questions for the given game name.
    /// Falls back to "questo gioco" when the name is null or whitespace.
    /// </summary>
    internal static List<string> GenerateTemplateQuestions(string? gameName)
    {
        var name = string.IsNullOrWhiteSpace(gameName) ? "questo gioco" : gameName;

        return
        [
            $"Come si svolge un turno in {name}?",
            $"Quali sono le regole di base di {name}?",
            $"Come si vince a {name}?",
            $"Quanti giocatori possono giocare a {name}?",
            $"Quanto dura una partita a {name}?"
        ];
    }
}
