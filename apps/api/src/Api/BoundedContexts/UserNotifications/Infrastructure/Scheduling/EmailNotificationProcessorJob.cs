using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Email;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job for processing queued EMAIL notifications from the shared
/// <c>notification_queue_items</c> table (channel_type=email).
///
/// Issue #3026: prior to this job, <c>NotificationDispatcher</c> enqueued email-channel items into
/// <c>notification_queue_items</c> but the only drainer of that table
/// (<c>SlackNotificationProcessorJob</c>) fetched Slack channels only — so email items were orphaned
/// and never sent (310 stuck on staging). This job mirrors the Slack processor: it drains pending
/// Email items, resolves the recipient address, renders via <see cref="EmailMessageBuilderFactory"/>,
/// and sends via <c>IEmailService.SendRawEmailAsync</c>. Reuses the <see cref="NotificationQueueItem"/>
/// retry/dead-letter state machine.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class EmailNotificationProcessorJob : IJob
{
    private readonly INotificationQueueRepository _queueRepository;
    private readonly EmailMessageBuilderFactory _builderFactory;
    private readonly IEmailService _emailService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<EmailNotificationProcessorJob> _logger;

    private const int BatchSize = 10;

    public EmailNotificationProcessorJob(
        INotificationQueueRepository queueRepository,
        EmailMessageBuilderFactory builderFactory,
        IEmailService emailService,
        MeepleAiDbContext dbContext,
        ILogger<EmailNotificationProcessorJob> logger)
    {
        _queueRepository = queueRepository ?? throw new ArgumentNullException(nameof(queueRepository));
        _builderFactory = builderFactory ?? throw new ArgumentNullException(nameof(builderFactory));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogDebug("Starting email notification processor job: FireTime={FireTime}", context.FireTimeUtc);

        var sentCount = 0;
        var failedCount = 0;
        var deadLetteredCount = 0;

        try
        {
            var pending = await _queueRepository
                .GetPendingByChannelAsync(NotificationChannelType.Email, BatchSize, context.CancellationToken)
                .ConfigureAwait(false);

            if (pending.Count == 0)
            {
                _logger.LogDebug("No pending email notifications to process");
                context.Result = new { Success = true, Sent = 0, Failed = 0, DeadLettered = 0 };
                return;
            }

            _logger.LogInformation("Processing {Count} pending email notifications", pending.Count);

            // Batch-resolve recipient addresses to avoid an N+1 lookup per item
            // (mirrors SendManualNotificationCommandHandler's email-resolution pattern).
            var userIds = pending
                .Where(i => i.RecipientUserId.HasValue)
                .Select(i => i.RecipientUserId!.Value)
                .Distinct()
                .ToList();

            var recipientsById = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Email, u.DisplayName })
                .ToDictionaryAsync(u => u.Id, u => (u.Email, u.DisplayName), context.CancellationToken)
                .ConfigureAwait(false);

            foreach (var item in pending)
            {
                if (context.CancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    item.MarkAsProcessing();
                    await _queueRepository.UpdateAsync(item, context.CancellationToken).ConfigureAwait(false);
                    await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

                    string? address = null;
                    string? displayName = null;
                    if (item.RecipientUserId.HasValue
                        && recipientsById.TryGetValue(item.RecipientUserId.Value, out var recipient))
                    {
                        address = recipient.Email;
                        displayName = recipient.DisplayName;
                    }

                    if (string.IsNullOrWhiteSpace(address))
                    {
                        // No address on file: dead-letter this one item (do NOT crash the batch).
                        await DeadLetterAsync(
                            item,
                            $"No email address on file for recipient user {item.RecipientUserId}",
                            context.CancellationToken).ConfigureAwait(false);
                        deadLetteredCount++;
                        continue;
                    }

                    var builder = _builderFactory.GetBuilder(item.NotificationType);
                    var email = builder.BuildMessage(new EmailBuildContext(
                        item.NotificationType,
                        item.Payload,
                        item.DeepLinkPath,
                        string.IsNullOrWhiteSpace(displayName) ? address : displayName));

                    await _emailService
                        .SendRawEmailAsync(address, email.Subject, email.HtmlBody, context.CancellationToken)
                        .ConfigureAwait(false);

                    item.MarkAsSent(DateTime.UtcNow);
                    await _queueRepository.UpdateAsync(item, context.CancellationToken).ConfigureAwait(false);
                    await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

                    sentCount++;
                    _logger.LogInformation(
                        "Email notification {ItemId} sent successfully to user {UserId}",
                        item.Id, item.RecipientUserId);
                }
#pragma warning disable CA1031 // best-effort delivery: a single item's failure must not abort the batch
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    failedCount++;
                    _logger.LogError(
                        ex, "Failed to send email notification {ItemId} to user {UserId}",
                        item.Id, item.RecipientUserId);
                    await HandleFailureAsync(item, ex.Message, context.CancellationToken).ConfigureAwait(false);
                }
            }

            _logger.LogInformation(
                "Email notification processor completed: Sent={Sent}, Failed={Failed}, DeadLettered={DeadLettered}",
                sentCount, failedCount, deadLetteredCount);

            context.Result = new { Success = true, Sent = sentCount, Failed = failedCount, DeadLettered = deadLetteredCount };
        }
#pragma warning disable CA1031 // top-level guard: a job must never surface an unhandled exception to Quartz
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Email notification processor job failed");
            context.Result = new { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Dead-letters a single item (no further retries) and persists the transition. Self-contained:
    /// swallows its own persistence errors so it never propagates into the per-item catch (which would
    /// otherwise try to MarkAsFailed on an already dead-lettered item).
    /// </summary>
    private async Task DeadLetterAsync(NotificationQueueItem item, string reason, CancellationToken ct)
    {
        try
        {
            item.MarkAsDeadLetter(reason);
            await _queueRepository.UpdateAsync(item, ct).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            _logger.LogWarning("Email notification {ItemId} dead-lettered: {Reason}", item.Id, reason);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Failed to dead-letter email notification {ItemId}", item.Id);
        }
    }

    /// <summary>
    /// Records a delivery failure with exponential-backoff retry scheduling (escalating to dead letter
    /// after MaxRetries). Mirrors SlackNotificationProcessorJob.HandleFailureAsync.
    /// </summary>
    private async Task HandleFailureAsync(NotificationQueueItem item, string errorMessage, CancellationToken ct)
    {
        try
        {
            item.MarkAsFailed(errorMessage);
            await _queueRepository.UpdateAsync(item, ct).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            if (item.Status.IsDeadLetter)
            {
                _logger.LogWarning(
                    "Email notification {ItemId} moved to dead letter after {RetryCount} retries",
                    item.Id, item.RetryCount);
            }
        }
#pragma warning disable CA1031
        catch (Exception updateEx)
#pragma warning restore CA1031
        {
            _logger.LogError(updateEx, "Failed to update email notification {ItemId} status after failure", item.Id);
        }
    }
}
