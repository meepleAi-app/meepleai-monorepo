using System.Text.Json.Nodes;
using Api.BoundedContexts.Authentication.Application.DTOs;
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
///
/// <para>
/// Transaction isolation: the logger resolves a fresh <see cref="MeepleAiDbContext"/>
/// from a child <see cref="IServiceScopeFactory"/> scope rather than
/// participating in the caller's ambient scope. Without this, an audit
/// row added in the caller's tracker would be rolled back together with
/// any subsequent failure in the caller's UnitOfWork — silently violating
/// the "audit on every attempt" guarantee that is the point of I10.
/// </para>
///
/// <para>
/// #3838 — impersonation: during an active impersonation the row is attributed to the real admin
/// (<see cref="Principal.EffectiveActor"/>), as <see cref="Principal"/> requires of every audit row, and
/// the impersonated user is kept in the metadata. A handler passes the user it acts for (the session's
/// subject, who owns the password or the devices), and cannot tell that someone else is acting.
/// </para>
/// </summary>
internal sealed class AuditLogger : IAuditLogger
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuditLogger> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AuditLogger(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<AuditLogger> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpContextAccessor = httpContextAccessor;
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

        var impersonation = ActiveImpersonation();
        if (impersonation is not null)
        {
            actorUserId = impersonation.Actor!.Id;
            metadata = WithImpersonatedSubject(metadata, impersonation.Subject.Id);
        }

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
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            dbContext.Set<AuditLogEntity>().Add(entry);
            // #3838: not the caller's token. Callers log after their action has committed: a request aborted in
            // between would lose the row for an action that happened, and throw out of a method that must not.
            await dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
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

    /// <summary>The principal of the current request when it is an active impersonation, else null.</summary>
    private Principal? ActiveImpersonation()
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        return httpContext?.Items.TryGetValue(nameof(SessionStatusDto), out var value) == true
            && value is SessionStatusDto { IsValid: true, Principal: { IsImpersonating: true } principal }
            ? principal
            : null;
    }

    /// <summary>Adds <c>impersonatedUserId</c> to the metadata object, creating it when there is none.</summary>
    private static string WithImpersonatedSubject(string? metadata, Guid subjectId)
    {
        JsonObject node;
        try
        {
            node = metadata is null ? new JsonObject() : JsonNode.Parse(metadata) as JsonObject ?? new JsonObject { ["metadata"] = metadata };
        }
        catch (System.Text.Json.JsonException)
        {
            node = new JsonObject { ["metadata"] = metadata };
        }

        node["impersonatedUserId"] = subjectId.ToString();
        return node.ToJsonString();
    }
}
