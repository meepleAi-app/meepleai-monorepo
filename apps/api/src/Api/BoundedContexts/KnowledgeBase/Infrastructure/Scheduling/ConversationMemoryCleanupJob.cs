using Api.BoundedContexts.KnowledgeBase.Application.Configuration;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job for cleaning up old conversation memories for GDPR compliance.
/// Issue #3498: Conversation Memory - Temporal RAG Implementation
/// Runs daily to delete memories older than the configured retention period (default: 90 days).
/// </summary>
[DisallowConcurrentExecution]
internal sealed class ConversationMemoryCleanupJob : IJob
{
    private readonly IConversationMemoryRepository _repository;
    private readonly IOptions<ConversationMemoryCleanupOptions> _options;
    private readonly ILogger<ConversationMemoryCleanupJob> _logger;

    public ConversationMemoryCleanupJob(
        IConversationMemoryRepository repository,
        IOptions<ConversationMemoryCleanupOptions> options,
        ILogger<ConversationMemoryCleanupJob> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var config = _options.Value;

        if (!config.Enabled)
        {
            _logger.LogDebug("Conversation memory cleanup job is disabled via configuration");
            return;
        }

        _logger.LogInformation(
            "Starting conversation memory cleanup job: RetentionPeriod={RetentionPeriod}, FireTime={FireTime}",
            config.RetentionPeriod, context.FireTimeUtc);

        try
        {
            var cutoffDate = DateTime.UtcNow - config.RetentionPeriod;

            var deletedCount = await _repository.DeleteOlderThanAsync(
                cutoffDate,
                context.CancellationToken).ConfigureAwait(false);

            // Note: DeleteOlderThanAsync uses ExecuteDeleteAsync which commits immediately
            // No need to call SaveChangesAsync

            _logger.LogInformation(
                "Conversation memory cleanup job completed successfully: DeletedCount={DeletedCount}, CutoffDate={CutoffDate}",
                deletedCount, cutoffDate);

            // Store execution result in job context for monitoring
            context.Result = new
            {
                Success = true,
                DeletedCount = deletedCount,
                CutoffDate = cutoffDate,
                RetentionPeriod = config.RetentionPeriod
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
        // Background tasks must not throw exceptions (would terminate task scheduler).
        // Errors logged for monitoring; task failures don't impact main application.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Conversation memory cleanup job failed: RetentionPeriod={RetentionPeriod}",
                config.RetentionPeriod);

            context.Result = new
            {
                Success = false,
                Error = ex.Message
            };

            // Don't rethrow - Quartz will mark job as failed
        }
#pragma warning restore CA1031
    }
}
