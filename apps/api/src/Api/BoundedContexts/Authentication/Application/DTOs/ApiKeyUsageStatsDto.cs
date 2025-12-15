namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// DTO for API key usage statistics.
/// </summary>
internal class ApiKeyUsageStatsDto
{
    /// <summary>
    /// API key ID.
    /// </summary>
    public Guid KeyId { get; set; }

    /// <summary>
    /// Total number of times the API key has been used.
    /// </summary>
    public int TotalUsageCount { get; set; }

    /// <summary>
    /// When the API key was last used (UTC).
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Usage count in the last 24 hours.
    /// </summary>
    public int UsageCountLast24Hours { get; set; }

    /// <summary>
    /// Usage count in the last 7 days.
    /// </summary>
    public int UsageCountLast7Days { get; set; }

    /// <summary>
    /// Usage count in the last 30 days.
    /// </summary>
    public int UsageCountLast30Days { get; set; }

    /// <summary>
    /// Average requests per day over the last 30 days.
    /// </summary>
    public double AverageRequestsPerDay { get; set; }
}
