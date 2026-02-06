using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to update PDF upload limits for a specific user tier.
/// Issue #3673: PDF Upload Limits Admin UI
/// </summary>
internal sealed record UpdatePdfLimitsCommand : ICommand<PdfLimitConfigDto>
{
    /// <summary>
    /// User tier to update: "free", "normal", or "premium"
    /// </summary>
    public required string Tier { get; init; }

    /// <summary>
    /// Maximum PDFs per day (must be > 0)
    /// </summary>
    public required int MaxPerDay { get; init; }

    /// <summary>
    /// Maximum PDFs per week (must be >= MaxPerDay)
    /// </summary>
    public required int MaxPerWeek { get; init; }

    /// <summary>
    /// Maximum PDFs per game for private uploads (must be > 0)
    /// </summary>
    public required int MaxPerGame { get; init; }

    /// <summary>
    /// Admin user ID executing this command
    /// </summary>
    public required Guid AdminUserId { get; init; }
}
