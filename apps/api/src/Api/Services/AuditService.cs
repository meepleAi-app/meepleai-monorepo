using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

public class AuditService
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<AuditService> _logger;
    private readonly string _tenantId;

    public AuditService(MeepleAiDbContext db, ILogger<AuditService> logger, IOptions<SingleTenantOptions> tenantOptions)
    {
        _db = db;
        _logger = logger;
        _tenantId = (tenantOptions?.Value ?? new SingleTenantOptions()).GetTenantId();
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
                TenantId = _tenantId,
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

    public async Task LogTenantAccessDeniedAsync(
        string userTenantId,
        string requestedTenantId,
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
            $"User from tenant {userTenantId} attempted to access {resource} in tenant {requestedTenantId}",
            ipAddress,
            userAgent,
            ct);

        _logger.LogWarning(
            "Tenant access denied: User {UserId} from tenant {UserTenantId} attempted to access {Resource} in tenant {RequestedTenantId}",
            userId, userTenantId, resource, requestedTenantId);
    }
}
