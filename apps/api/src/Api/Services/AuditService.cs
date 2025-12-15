using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

internal class AuditService
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<AuditService> _logger;
    private readonly TimeProvider _timeProvider;

    public AuditService(MeepleAiDbContext db, ILogger<AuditService> logger, TimeProvider? timeProvider = null)
    {
        _db = db;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task LogAsync(
        string? userId,
        string action,
        string resource,
        string? resourceId,
        string result,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
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
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
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

    public async Task LogAccessDeniedAsync(
        string userScope,
        string requiredScope,
        string userId,
        string resource,
        string? resourceId = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
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
            ct).ConfigureAwait(false);

        _logger.LogWarning(
            "Access denied: User {UserId} in scope {UserScope} attempted to access {Resource} requiring scope {RequiredScope}",
            userId, userScope, resource, requiredScope);
    }
}
