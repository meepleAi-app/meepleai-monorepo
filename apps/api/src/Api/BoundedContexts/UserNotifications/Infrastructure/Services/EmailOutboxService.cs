using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Infrastructure.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Services;

/// <summary>
/// I5 (auth security fixes): default <see cref="IEmailOutboxService"/>
/// implementation. Persists rows to the <c>email_outbox</c> table; the
/// background processor (<c>EmailOutboxBackgroundService</c>) is
/// responsible for actually invoking SMTP.
///
/// Idempotency is enforced by the unique index on
/// <see cref="EmailOutboxEntity.IdempotencyKey"/>. A duplicate insert
/// surfaces as a unique-violation, which we catch and translate to a
/// no-op return value of <c>false</c> — callers can treat both branches
/// as success because the email is already (or has been) enqueued.
/// </summary>
internal sealed class EmailOutboxService : IEmailOutboxService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EmailOutboxService> _logger;

    public EmailOutboxService(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<EmailOutboxService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> EnqueueAsync(
        string toEmail,
        string subject,
        string bodyHtml,
        string idempotencyKey,
        DateTime? scheduledAt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(bodyHtml);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var entry = new EmailOutboxEntity
        {
            Id = Guid.NewGuid(),
            ToEmail = toEmail,
            Subject = subject,
            BodyHtml = bodyHtml,
            IdempotencyKey = idempotencyKey,
            ScheduledAt = scheduledAt ?? now,
            CreatedAt = now,
            Status = "Pending",
        };

        try
        {
            _dbContext.EmailOutbox.Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Enqueued email {IdempotencyKey} for {ToEmail}",
                idempotencyKey,
                DataMasking.MaskEmail(toEmail));
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // The detached entity must be removed from the change tracker
            // so the next SaveChanges (potentially on the same scoped
            // context) doesn't retry the insert.
            _dbContext.Entry(entry).State = EntityState.Detached;
            _logger.LogInformation(
                ex,
                "Email {IdempotencyKey} already enqueued — skipping duplicate insert.",
                idempotencyKey);
            return false;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        if (ex.InnerException is Npgsql.PostgresException pgEx)
        {
            return string.Equals(pgEx.SqlState, "23505", StringComparison.Ordinal);
        }
        return false;
    }
}
