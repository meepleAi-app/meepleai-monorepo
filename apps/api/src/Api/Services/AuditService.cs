using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.Administration.Application;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

internal class AuditService
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<AuditService> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// JSON options for serializing <see cref="AuditOutboxPayload"/>. PascalCase keys are kept
    /// intentionally so the T4 processor can round-trip with default JsonSerializer options.
    /// Null values are omitted to keep payload_json compact.
    /// </summary>
    private static readonly JsonSerializerOptions AuditOutboxJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public AuditService(MeepleAiDbContext db, ILogger<AuditService> logger, TimeProvider? timeProvider = null)
    {
        _db = db;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public virtual async Task LogAsync(
        string? userId,
        string action,
        string resource,
        string? resourceId,
        string result,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        AuditLogEntity? added = null;
        try
        {
            var userGuid = !string.IsNullOrWhiteSpace(userId) && Guid.TryParse(userId, out var parsed) ? (Guid?)parsed : null;

            // #5156: cut to the columns — the User-Agent, for one, is the client's header.
            var auditLog = new AuditLogEntity
            {
                UserId = userGuid,
                Action = action,
                Resource = resource,
                ResourceId = resourceId,
                Result = result,
                Details = details,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            }.FitToColumns();

            _db.AuditLogs.Add(auditLog);
            added = auditLog;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // #5156: the context is the caller's. Left Added, the refused row would be written again by the caller's
            // next SaveChanges, and fail it: the audit would break the business operation after all. Only a row that
            // was added is detached, so that nothing here can throw when the add itself was what failed.
            if (added is not null)
            {
                _db.Entry(added).State = EntityState.Detached;
            }

            // RESILIENCE PATTERN: Audit logging must never fail business operations
            // Rationale: Auditing is a secondary concern - failing a user request because
            // we cannot write an audit log violates fail-safe principles. We log the error
            // for monitoring but allow the primary operation to succeed.
            // Context: Audit logging failures are typically DB-related (connection loss, disk full)
            _logger.LogError(ex, "Failed to write audit log for action {Action} on {Resource}", action, resource);
        }
    }

    public virtual async Task LogAccessDeniedAsync(
        string userScope,
        string requiredScope,
        string userId,
        string resource,
        string? resourceId = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var details = $"User in scope {userScope} attempted to access {resource} requiring scope {requiredScope}";

        await LogAsync(
            userId,
            "ACCESS_DENIED",
            resource,
            resourceId,
            "Denied",
            details,
            ipAddress,
            userAgent,
            cancellationToken).ConfigureAwait(false);

        _logger.LogWarning(
            "Access denied: User {UserId} in scope {UserScope} attempted to access {Resource} requiring scope {RequiredScope}",
            userId, userScope, resource, requiredScope);
    }

    /// <summary>
    /// Writes an <see cref="AuditOutboxEntity"/> (Status=Pending) to the audit_outbox table.
    /// The T4 AuditOutboxProcessor later materializes this into a permanent <see cref="AuditLogEntity"/>.
    ///
    /// Resilience: failures are logged and swallowed — audit enqueue must never break business ops (best-effort, T3).
    /// Atomicity: this is a SEPARATE SaveChanges from the handler's business SaveChanges (T3b will address
    /// atomic writes for destructive commands via the AtomicAudit pattern).
    /// </summary>
    public virtual async Task EnqueueAuditAsync(
        AuditOutboxPayload payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload, AuditOutboxJsonOptions);
            var row = AuditOutboxEntity.CreatePending(json, _timeProvider.GetUtcNow());
            _db.AuditOutbox.Add(row);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Resilience: audit failures must never break business operations (best-effort, T3)
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex,
                "Failed to enqueue audit outbox row for action {Action} on {Resource}",
                payload.Action, payload.Resource);
        }
    }

    /// <summary>
    /// Atomic-path outbox write: adds the Pending row and flushes via SaveChanges (which, inside an
    /// open transaction, writes to the transaction but does NOT commit it — commit is the caller's
    /// responsibility). Any failure propagates to the caller so its enclosing transaction rolls back
    /// the mutation + this outbox row together.
    ///
    /// Unlike <see cref="EnqueueAuditAsync"/> this does NOT swallow exceptions — audit loss on a
    /// destructive command must fail the whole operation. The caller (AuditLoggingBehavior atomic
    /// path) wraps this in CreateExecutionStrategy().ExecuteAsync + BeginTransactionAsync so that
    /// both mutation and audit row are committed atomically or rolled back entirely.
    ///
    /// SP5 Admin Security S1 — Task 3b.
    /// </summary>
    public virtual async Task EnqueueAuditAtomicAsync(
        AuditOutboxPayload payload,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(payload, AuditOutboxJsonOptions);
        var row = AuditOutboxEntity.CreatePending(json, _timeProvider.GetUtcNow());
        _db.AuditOutbox.Add(row);
        // SaveChanges flushes the INSERT to the current open transaction; the caller commits.
        // Exceptions propagate — the enclosing transaction rolls back mutation + outbox row.
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
