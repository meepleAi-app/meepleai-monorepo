using Api.BoundedContexts.SecurityAudit.Application.Services;
using Api.BoundedContexts.SecurityAudit.Infrastructure.Entities;
using Api.Infrastructure;

namespace Api.BoundedContexts.SecurityAudit.Infrastructure.Services;

/// <summary>
/// I10 (auth security fixes): default <see cref="IAuditLogger"/>
/// implementation. Persists rows to the <c>security_audit_logs</c> table.
///
/// <para>
/// Failure isolation: any exception out of the underlying SaveChanges is
/// caught, logged at <c>Error</c>, and swallowed. Audit logging must
/// never fail the caller's domain transaction — losing an audit row
/// is preferable to losing a legitimate user action because the audit
/// store is degraded.
/// </para>
/// </summary>
internal sealed class AuditLogger : IAuditLogger
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider,
        ILogger<AuditLogger> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LogAsync(
        string eventType,
        Guid? actorUserId = null,
        Guid? targetUserId = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? metadata = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        var entry = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            ActorUserId = actorUserId,
            TargetUserId = targetUserId,
            EventType = eventType,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            Metadata = metadata,
            CorrelationId = correlationId,
        };

#pragma warning disable CA1031 // Do not catch general exception types
        // AUDIT BOUNDARY: an exception here must NEVER fail the caller's
        // domain transaction. Audit storage degradation is observable in
        // logs (LogError below) and via monitoring on the audit table
        // size; losing a row is the lesser evil compared to refusing
        // legitimate user actions.
        try
        {
            _dbContext.Set<AuditLogEntity>().Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Failed to persist audit-log entry {EventType} for actor={ActorUserId} target={TargetUserId}",
                eventType,
                actorUserId,
                targetUserId);
        }
#pragma warning restore CA1031
    }
}
