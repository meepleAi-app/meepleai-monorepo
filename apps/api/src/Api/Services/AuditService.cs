using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class AuditService
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<AuditService> _logger;
    public AuditService(MeepleAiDbContext db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
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
            var auditLog = new AuditLogEntity
            {
                UserId = userId,
                Action = action,
                Resource = resource,
                ResourceId = resourceId,
                Result = result,
                Details = details,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow
            };

            _db.AuditLogs.Add(auditLog);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Don't fail the request if audit logging fails
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
        await LogAsync(
            userId,
            "ACCESS_DENIED",
            resource,
            resourceId,
            "Denied",
            $"User in scope {userScope} attempted to access {resource} requiring scope {requiredScope}",
            ipAddress,
            userAgent,
            ct);

        _logger.LogWarning(
            "Access denied: User {UserId} in scope {UserScope} attempted to access {Resource} requiring scope {RequiredScope}",
            userId, userScope, resource, requiredScope);
    }
}
