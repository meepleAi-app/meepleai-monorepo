using Api.BoundedContexts.UserNotifications.Infrastructure.Entities;
using Api.Infrastructure;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// I5 (auth security fixes): drains the <c>email_outbox</c> table.
///
/// On each tick (default 30 seconds) the service:
///   1. Picks up to <see cref="BatchSize"/> rows where status='Pending'
///      and scheduled_at &lt;= now, ordered oldest-first.
///   2. For each row, invokes <see cref="IEmailService.SendRawEmailAsync"/>.
///   3. On success: marks the row Sent + sets sent_at.
///   4. On failure: increments attempt_count, computes the next
///      scheduled_at via exponential backoff (1m, 5m, 30m, 2h, 6h),
///      and stores LastError. After <see cref="MaxAttempts"/> failures
///      the row is marked FailedPermanent for ops review.
///
/// Cleanup of long-since-Sent / FailedPermanent rows is intentionally
/// out of scope — the dedicated retention job handles that. This service
/// is responsible only for transitioning Pending → {Sent | FailedPermanent}.
///
/// All exceptions out of <see cref="IEmailService.SendRawEmailAsync"/>
/// are caught and treated as transient. Permanent failure semantics are
/// derived from attempt_count rather than exception type because the
/// underlying SMTP layer can throw a permanent-looking error for a
/// transient cause (e.g. a temporarily-rejected sender domain).
/// </summary>
internal sealed class EmailOutboxBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 25;
    private const int MaxAttempts = 5;

    /// <summary>
    /// Per-attempt backoff schedule. <c>BackoffSchedule[i]</c> is the delay
    /// after the (i+1)-th failure. Anything past the last entry uses the
    /// last entry — the row will be FailedPermanent before then anyway
    /// because <see cref="MaxAttempts"/> bounds the loop.
    /// </summary>
    private static readonly TimeSpan[] BackoffSchedule =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6),
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EmailOutboxBackgroundService> _logger;

    public EmailOutboxBackgroundService(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<EmailOutboxBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "EmailOutboxBackgroundService started. Poll interval: {Interval}s, batch size: {BatchSize}, max attempts: {MaxAttempts}",
            PollInterval.TotalSeconds,
            BatchSize,
            MaxAttempts);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainOnceAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // BACKGROUND SERVICE BOUNDARY: a transient error inside the
            // drain loop must not crash the host. Log + continue; the
            // next poll tick retries the row whose attempt failed.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error during email-outbox drain");
            }
#pragma warning restore CA1031

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task DrainOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Fetch the next batch of due rows. The (Status, ScheduledAt) index
        // (see EmailOutboxEntityConfiguration) makes this query cheap even
        // as the table grows.
        var due = await dbContext.EmailOutbox
            .Where(e => e.Status == "Pending" && e.ScheduledAt <= now)
            .OrderBy(e => e.ScheduledAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (due.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Email outbox: draining {Count} pending row(s)", due.Count);

        foreach (var row in due)
        {
            await TrySendAsync(row, dbContext, emailService, cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task TrySendAsync(
        EmailOutboxEntity row,
        MeepleAiDbContext dbContext,
        IEmailService emailService,
        CancellationToken cancellationToken)
    {
        try
        {
            await emailService
                .SendRawEmailAsync(row.ToEmail, row.Subject, row.BodyHtml, cancellationToken)
                .ConfigureAwait(false);

            row.Status = "Sent";
            row.SentAt = _timeProvider.GetUtcNow().UtcDateTime;
            row.LastError = null;
            row.AttemptCount += 1;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            row.AttemptCount += 1;
            row.LastError = TruncateError(ex.Message);

            if (row.AttemptCount >= MaxAttempts)
            {
                row.Status = "FailedPermanent";
                _logger.LogError(
                    ex,
                    "Email outbox row {RowId} marked FailedPermanent after {Attempts} attempts. " +
                    "Idempotency key: {Key}",
                    row.Id,
                    row.AttemptCount,
                    row.IdempotencyKey);
            }
            else
            {
                var backoffIndex = Math.Min(row.AttemptCount - 1, BackoffSchedule.Length - 1);
                var nextAttempt = _timeProvider.GetUtcNow().UtcDateTime + BackoffSchedule[backoffIndex];

                // ScheduledAt is init-only on the entity for caller safety —
                // mutate via a tracked property explicitly. We keep it
                // mutable in the persistence model for the processor's
                // benefit (set via reflection-friendly property on the
                // entity). To avoid changing the entity surface, we
                // overwrite the in-memory row using EF tracking by detaching
                // and re-attaching with the new timestamp would be heavy;
                // simpler: bump the ScheduledAt via the change tracker.
                dbContext.Entry(row).Property(nameof(EmailOutboxEntity.ScheduledAt)).CurrentValue = nextAttempt;

                _logger.LogWarning(
                    ex,
                    "Email outbox row {RowId} attempt {Attempt}/{Max} failed; rescheduled to {NextAttempt}",
                    row.Id,
                    row.AttemptCount,
                    MaxAttempts,
                    nextAttempt);
            }
        }
#pragma warning restore CA1031
    }

    private static string TruncateError(string message)
    {
        const int MaxLen = 1990; // LastError is HasMaxLength(2000) — leave headroom for an ellipsis.
        return message.Length <= MaxLen ? message : message[..MaxLen] + "...";
    }
}
