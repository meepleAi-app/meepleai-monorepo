using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Issue #1823 Wave 3 retention sweep (ADR DEC-3j: dead-letter retention 7
/// days). Quartz job that runs daily at 03:00 UTC and deletes
/// <see cref="Domain.Aggregates.WikidataCoverEnrichmentAttempt"/> rows whose
/// <c>DeadLetteredAt</c> timestamp is older than
/// <see cref="RetentionWindow"/>.
/// </summary>
/// <remarks>
/// <para>Single-pod batch (HPA=1 per DEC-3e). <c>[DisallowConcurrentExecution]</c>
/// belt-and-braces against overlapping ticks. Internal <see cref="RunOnceAsync"/>
/// hook for unit tests so the Quartz scheduler does not need to spin up.</para>
///
/// <para>Idempotent: re-running the sweep on the same day deletes nothing new
/// because previous rows with old <c>DeadLetteredAt</c> have already been
/// removed.</para>
///
/// <para>Records the deleted count as a structured log line so ops can spot
/// retention spikes. The M11 metric expansion will add a Prometheus counter
/// for this signal.</para>
/// </remarks>
[DisallowConcurrentExecution]
public sealed class WikidataCoverDeadLetterRetentionJob : IJob
{
    /// <summary>Dead-letter retention window per ADR DEC-3j.</summary>
    public static readonly TimeSpan RetentionWindow = TimeSpan.FromDays(7);

    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WikidataCoverDeadLetterRetentionJob> _logger;

    public WikidataCoverDeadLetterRetentionJob(
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        ILogger<WikidataCoverDeadLetterRetentionJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;

        using var scope = _serviceProvider.CreateScope();
        var attempts = scope.ServiceProvider.GetRequiredService<IWikidataCoverEnrichmentAttemptRepository>();

        await RunOnceAsync(attempts, ct).ConfigureAwait(false);
    }

    /// <summary>Internal sweep — extracted from <see cref="Execute"/> for unit tests.</summary>
    internal async Task<int> RunOnceAsync(
        IWikidataCoverEnrichmentAttemptRepository attempts,
        CancellationToken ct)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var cutoff = nowUtc - RetentionWindow;

        _logger.LogDebug(
            "WikidataCoverDeadLetterRetentionJob: sweeping dead-letter rows older than {Cutoff} (retention {Days}d).",
            cutoff, RetentionWindow.TotalDays);

        var deleted = await attempts
            .DeleteDeadLetteredOlderThanAsync(cutoff, ct)
            .ConfigureAwait(false);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "WikidataCoverDeadLetterRetentionJob: deleted {Count} dead-letter row(s) older than {Cutoff}.",
                deleted, cutoff);
        }
        else
        {
            _logger.LogDebug(
                "WikidataCoverDeadLetterRetentionJob: no dead-letter rows older than {Cutoff} — sweep idle.",
                cutoff);
        }

        return deleted;
    }
}
