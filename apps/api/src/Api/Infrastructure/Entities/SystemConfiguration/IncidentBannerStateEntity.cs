namespace Api.Infrastructure.Entities.SystemConfiguration;

/// <summary>
/// Issue #1089: Persistence entity for the global incident banner.
/// Singleton row (one row per DB).
/// </summary>
public sealed class IncidentBannerStateEntity
{
    public Guid Id { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Stored as int (enum BannerSeverity: 0=Info, 1=Warning, 2=Critical).
    /// </summary>
    public int Severity { get; set; }

    public bool IsActive { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
