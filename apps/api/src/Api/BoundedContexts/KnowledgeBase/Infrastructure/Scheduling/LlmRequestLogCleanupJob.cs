using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job that deletes LlmRequestLog entries older than 30 days.
/// Issue #5072: OpenRouter monitoring — automatic 30-day retention enforcement.
/// Runs daily at 4:00 AM UTC (after conversation memory cleanup at 3 AM).
/// </summary>
[DisallowConcurrentExecution]
internal sealed class LlmRequestLogCleanupJob : IJob
{
    private readonly ILlmRequestLogRepository _repository;
    private readonly ILogger<LlmRequestLogCleanupJob> _logger;

    public LlmRequestLogCleanupJob(
        ILlmRequestLogRepository repository,
        ILogger<LlmRequestLogCleanupJob> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "Starting LLM request log cleanup job: FireTime={FireTime}",
            context.FireTimeUtc);

        try
        {
            var cutoff = DateTime.UtcNow; // ExpiresAt < now() — records set their own 30-day window

            var deletedCount = await _repository.DeleteExpiredAsync(
                cutoff,
                context.CancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "LLM request log cleanup completed: DeletedCount={DeletedCount}",
                deletedCount);

            context.Result = new { Success = true, DeletedCount = deletedCount };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125
        // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
        // Background tasks must not throw exceptions (would terminate task scheduler).
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM request log cleanup job failed");
            context.Result = new { Success = false, Error = ex.Message };
        }
#pragma warning restore CA1031
    }
}
