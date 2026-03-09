using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job that pseudonymizes UserId in LLM request logs older than 7 days.
/// Issue #5511: GDPR compliance — replaces UserId with salted SHA-256 hash.
/// Runs daily at 2:00 AM UTC.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class LlmRequestLogPseudonymizationJob : IJob
{
    /// <summary>
    /// Number of days after which logs are pseudonymized.
    /// </summary>
    internal const int RetentionDays = 7;

    private readonly ILlmRequestLogRepository _repository;
    private readonly IOptions<LlmRequestLogPseudonymizationOptions> _options;
    private readonly ILogger<LlmRequestLogPseudonymizationJob> _logger;

    public LlmRequestLogPseudonymizationJob(
        ILlmRequestLogRepository repository,
        IOptions<LlmRequestLogPseudonymizationOptions> options,
        ILogger<LlmRequestLogPseudonymizationJob> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "Starting LLM request log pseudonymization job: FireTime={FireTime}",
            context.FireTimeUtc);

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
            var salt = _options.Value.Salt;

            var pseudonymizedCount = await _repository.PseudonymizeOldLogsAsync(
                cutoff,
                salt,
                context.CancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "LLM request log pseudonymization completed: PseudonymizedCount={PseudonymizedCount}, CutoffDays={CutoffDays}",
                pseudonymizedCount, RetentionDays);

            context.Result = new { Success = true, PseudonymizedCount = pseudonymizedCount };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125
        // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
        // Background tasks must not throw exceptions (would terminate task scheduler).
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM request log pseudonymization job failed");
            context.Result = new { Success = false, Error = ex.Message };
        }
#pragma warning restore CA1031
    }
}
