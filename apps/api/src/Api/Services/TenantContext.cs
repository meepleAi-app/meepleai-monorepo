using System.Security.Claims;
using Microsoft.Extensions.Options;

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

public class TenantContextOptions
{
    /// <summary>
    /// Default tenant identifier used when no tenant is associated with the request.
    /// </summary>
    public string DefaultTenantId { get; set; } = "meepleai";
}

public class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TenantContextOptions _options;

    public TenantContext(IHttpContextAccessor httpContextAccessor, IOptions<TenantContextOptions> options)
    {
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public string? TenantId
    {
        get
        {
            var claimValue = _httpContextAccessor.HttpContext?.User
                ?.FindFirst("tenant")?.Value;

            if (!string.IsNullOrWhiteSpace(claimValue))
            {
                return claimValue;
            }

            return string.IsNullOrWhiteSpace(_options.DefaultTenantId)
                ? null
                : _options.DefaultTenantId.Trim();
        }
    }

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
