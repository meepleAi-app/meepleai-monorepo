using Api.BoundedContexts.Authentication.Application.DTOs;

namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// DTO combining API key details with usage statistics.
/// Used in admin dashboards for comprehensive key management.
/// </summary>
internal class ApiKeyWithStatsDto
{
    /// <summary>
    /// API key details.
    /// </summary>
    public required ApiKeyDto ApiKey { get; set; }

    /// <summary>
    /// Usage statistics for this API key.
    /// </summary>
    public required ApiKeyUsageStatsDto UsageStats { get; set; }
}
