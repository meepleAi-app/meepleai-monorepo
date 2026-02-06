namespace Api.BoundedContexts.SystemConfiguration.Application.DTOs;

/// <summary>
/// DTO representing PDF upload limits for a single user tier.
/// Issue #3673: PDF Upload Limits Admin UI (per-tier)
/// </summary>
public sealed record PdfLimitConfigDto
{
    /// <summary>
    /// User tier: "free", "normal", or "premium"
    /// </summary>
    public required string Tier { get; init; }

    /// <summary>
    /// Maximum PDFs that can be uploaded per day
    /// </summary>
    public required int MaxPerDay { get; init; }

    /// <summary>
    /// Maximum PDFs that can be uploaded per week
    /// </summary>
    public required int MaxPerWeek { get; init; }

    /// <summary>
    /// Maximum PDFs per game (private PDFs)
    /// </summary>
    public required int MaxPerGame { get; init; }

    /// <summary>
    /// When these limits were last updated (null if using defaults)
    /// </summary>
    public DateTime? UpdatedAt { get; init; }

    /// <summary>
    /// User ID who last updated these limits (null if using defaults)
    /// </summary>
    public string? UpdatedBy { get; init; }
}
