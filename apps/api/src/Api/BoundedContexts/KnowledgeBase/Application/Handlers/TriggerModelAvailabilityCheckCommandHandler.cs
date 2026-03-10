using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Issue #5503: Triggers an immediate model availability check via Quartz scheduler.
/// Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
internal sealed class TriggerModelAvailabilityCheckCommandHandler
    : ICommandHandler<TriggerModelAvailabilityCheckCommand, TriggerModelAvailabilityCheckResult>
{
    private static readonly JobKey ModelCheckJobKey = new("model-availability-check-job", "knowledge-base");

    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<TriggerModelAvailabilityCheckCommandHandler> _logger;

    public TriggerModelAvailabilityCheckCommandHandler(
        ISchedulerFactory schedulerFactory,
        ILogger<TriggerModelAvailabilityCheckCommandHandler> logger)
    {
        _schedulerFactory = schedulerFactory ?? throw new ArgumentNullException(nameof(schedulerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TriggerModelAvailabilityCheckResult> Handle(
        TriggerModelAvailabilityCheckCommand command,
        CancellationToken cancellationToken)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);

        var jobExists = await scheduler.CheckExists(ModelCheckJobKey, cancellationToken).ConfigureAwait(false);
        if (!jobExists)
        {
            _logger.LogWarning("ModelAvailabilityCheckJob not found in scheduler");
            return new TriggerModelAvailabilityCheckResult(false, "Model availability check job not found");
        }

        await scheduler.TriggerJob(ModelCheckJobKey, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Admin triggered immediate model availability check");
        return new TriggerModelAvailabilityCheckResult(true, "Model availability check triggered");
    }
}
