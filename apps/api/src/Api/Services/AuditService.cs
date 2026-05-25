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
        try
        {
            var userGuid = !string.IsNullOrWhiteSpace(userId) && Guid.TryParse(userId, out var parsed) ? (Guid?)parsed : null;

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
            };

            _db.AuditLogs.Add(auditLog);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
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
}
