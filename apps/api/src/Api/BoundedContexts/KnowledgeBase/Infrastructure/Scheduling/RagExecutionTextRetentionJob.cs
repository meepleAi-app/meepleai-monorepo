using Api.BoundedContexts.KnowledgeBase.Application.Configuration;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job that blanks the texts of old <c>rag_executions</c> rows.
/// Issue #4634: the playground saves, for every run, the question and a trace with previews of the retrieved passages
/// (content derived from the indexed PDFs) with no expiry.
/// Decision (2026-10-26, same remedy as #4311): an expiry for everyone that blanks the texts and keeps the rows, so
/// the RAG dashboard and the per-agent statistics, which work on the numeric columns, do not change.
/// Runs daily at 5:00 AM UTC. Rows older than the configured retention (default: 90 days) lose <c>query</c> and
/// <c>execution_trace</c>.
/// </summary>
/// <remarks>
/// Not <see cref="IRagExecutionRepository.DeleteOlderThanAsync"/>: that one deletes the rows, and with them the
/// metrics the dashboard reads. A retention of zero days or less is a misconfiguration, not "blank everything":
/// the job logs a warning and touches nothing.
/// </remarks>
[DisallowConcurrentExecution]
internal sealed class RagExecutionTextRetentionJob : IJob
{
    private readonly IRagExecutionRepository _repository;
    private readonly IOptions<RagExecutionTextRetentionOptions> _options;
    private readonly ILogger<RagExecutionTextRetentionJob> _logger;

    public RagExecutionTextRetentionJob(
        IRagExecutionRepository repository,
        IOptions<RagExecutionTextRetentionOptions> options,
        ILogger<RagExecutionTextRetentionJob> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            // The options are read inside the try: a value that does not bind (a non-numeric RetentionDays from an
            // environment variable) throws on .Value, and a background job must not take the scheduler down.
            var retentionDays = _options.Value.RetentionDays;

            if (retentionDays <= 0)
            {
                _logger.LogWarning(
                    "RAG execution text retention job: RetentionDays={RetentionDays} is not a positive number of days, nothing is blanked",
                    retentionDays);
                return;
            }

            _logger.LogInformation(
                "Starting RAG execution text retention job: RetentionDays={RetentionDays}, FireTime={FireTime}",
                retentionDays, context.FireTimeUtc);

            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

            var blankedCount = await _repository.BlankTextsOlderThanAsync(
                cutoff,
                RagExecutionTextRetentionOptions.BatchSize,
                context.CancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "RAG execution text retention job completed: BlankedCount={BlankedCount}, Cutoff={Cutoff}",
                blankedCount, cutoff);

            context.Result = new { Success = true, BlankedCount = blankedCount, Cutoff = cutoff };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
        // Background tasks must not throw exceptions (would terminate task scheduler).
        // A shutdown cancels the token: that is not a failed round, and Quartz has to see it.
#pragma warning restore S125
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "RAG execution text retention job failed");

            context.Result = new { Success = false, Error = ex.Message };
        }
#pragma warning restore CA1031
    }
}
