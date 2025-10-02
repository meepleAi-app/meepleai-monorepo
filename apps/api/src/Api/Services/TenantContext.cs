using System.Security.Claims;

namespace Api.Services;

/// <summary>
/// Provides access to the current tenant context from the authenticated user
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant ID from the authenticated user, or null if not authenticated
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Gets the current tenant ID, throwing if not authenticated
    /// </summary>
    string GetRequiredTenantId();
}

public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? TenantId => _httpContextAccessor.HttpContext?.User
        ?.FindFirst("tenant")?.Value;

    public string GetRequiredTenantId()
    {
        var tenantId = TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new UnauthorizedAccessException("No tenant context available");
        }
        return tenantId;
    }
}
