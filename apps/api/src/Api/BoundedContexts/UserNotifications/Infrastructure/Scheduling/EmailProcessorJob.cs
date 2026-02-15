using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Infrastructure;
using Api.Services;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job for processing queued emails with retry policy.
/// Runs every 30 seconds, processes up to 10 emails per execution.
/// Issue #4417: Email notification queue with retry and dead letter handling.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class EmailProcessorJob : IJob
{
    private readonly IEmailQueueRepository _emailQueueRepository;
    private readonly IEmailService _emailService;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<EmailProcessorJob> _logger;

    private const int BatchSize = 10;

    public EmailProcessorJob(
        IEmailQueueRepository emailQueueRepository,
        IEmailService emailService,
        MeepleAiDbContext dbContext,
        ILogger<EmailProcessorJob> logger)
    {
        _emailQueueRepository = emailQueueRepository ?? throw new ArgumentNullException(nameof(emailQueueRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("Starting email processor job: FireTime={FireTime}", context.FireTimeUtc);

        var sentCount = 0;
        var failedCount = 0;

        try
        {
            var pendingEmails = await _emailQueueRepository
                .GetPendingAsync(BatchSize, context.CancellationToken)
                .ConfigureAwait(false);

            if (pendingEmails.Count == 0)
            {
                _logger.LogDebug("No pending emails to process");
                context.Result = new { Success = true, Sent = 0, Failed = 0 };
                return;
            }

            _logger.LogInformation("Processing {Count} pending emails", pendingEmails.Count);

            foreach (var email in pendingEmails)
            {
                if (context.CancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    email.MarkAsProcessing();
                    await _emailQueueRepository.UpdateAsync(email, context.CancellationToken).ConfigureAwait(false);
                    await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

                    await _emailService.SendRawEmailAsync(
                        email.To,
                        email.Subject,
                        email.HtmlBody,
                        context.CancellationToken).ConfigureAwait(false);

                    email.MarkAsSent(DateTime.UtcNow);
                    await _emailQueueRepository.UpdateAsync(email, context.CancellationToken).ConfigureAwait(false);
                    await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

                    sentCount++;
                    _logger.LogInformation("Email {EmailId} sent successfully to {To}", email.Id, email.To);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    failedCount++;
                    _logger.LogError(ex, "Failed to send email {EmailId} to {To}", email.Id, email.To);

                    try
                    {
                        email.MarkAsFailed(ex.Message);
                        await _emailQueueRepository.UpdateAsync(email, context.CancellationToken).ConfigureAwait(false);
                        await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

                        if (email.Status.IsDeadLetter)
                        {
                            _logger.LogWarning(
                                "Email {EmailId} moved to dead letter after {RetryCount} retries",
                                email.Id, email.RetryCount);
                        }
                    }
#pragma warning disable CA1031
                    catch (Exception updateEx)
#pragma warning restore CA1031
                    {
                        _logger.LogError(updateEx, "Failed to update email {EmailId} status after send failure", email.Id);
                    }
                }
            }

            _logger.LogInformation(
                "Email processor completed: Sent={Sent}, Failed={Failed}",
                sentCount, failedCount);

            context.Result = new { Success = true, Sent = sentCount, Failed = failedCount };
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Email processor job failed");
            context.Result = new { Success = false, Error = ex.Message };
        }
    }
}
