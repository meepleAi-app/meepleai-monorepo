// Queries for session export (Issue #3347).

using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

// ============================================================================
// Export Session PDF Query
// ============================================================================

/// <summary>
/// Query to export a session as PDF.
/// </summary>
public sealed record ExportSessionPdfQuery(
    Guid SessionId,
    Guid RequestedBy,
    bool IncludeScoreChart = true,
    bool IncludeDiceHistory = false,
    bool IncludeCardHistory = false
) : IRequest<ExportSessionPdfResponse>;

/// <summary>
/// Response containing PDF bytes.
/// </summary>
public sealed record ExportSessionPdfResponse(
    byte[] PdfContent,
    string FileName,
    string ContentType
);

// ============================================================================
// Get Shareable Session Query
// ============================================================================

/// <summary>
/// Query to get shareable session summary (public, no auth required).
/// </summary>
public sealed record GetShareableSessionQuery(
    Guid SessionId
) : IRequest<ShareableSessionResponse>;

/// <summary>
/// Shareable session summary for public viewing.
/// </summary>
public sealed record ShareableSessionResponse(
    Guid SessionId,
    string SessionCode,
    string? GameName,
    string? GameImageUrl,
    DateTime SessionDate,
    TimeSpan? Duration,
    string? Location,
    string Status,
    List<ShareableParticipantDto> Participants,
    ShareableStatsDto Stats,
    DateTime? FinalizedAt
);

/// <summary>
/// Participant data for shareable view.
/// </summary>
public sealed record ShareableParticipantDto(
    string DisplayName,
    int? FinalRank,
    decimal? TotalScore,
    bool IsWinner
);

/// <summary>
/// Session statistics for shareable view.
/// </summary>
public sealed record ShareableStatsDto(
    int TotalParticipants,
    int TotalScoreEntries,
    int TotalDiceRolls,
    int TotalCardDraws
);

// ============================================================================
// Generate Session Share Link Query
// ============================================================================

/// <summary>
/// Query to generate a shareable link for a session.
/// </summary>
public sealed record GenerateSessionShareLinkQuery(
    Guid SessionId,
    Guid RequestedBy
) : IRequest<SessionShareLinkResponse>;

/// <summary>
/// Response with shareable link and OG metadata.
/// </summary>
public sealed record SessionShareLinkResponse(
    string ShareUrl,
    OpenGraphMetadata OgMetadata
);

/// <summary>
/// Open Graph metadata for social sharing.
/// </summary>
public sealed record OpenGraphMetadata(
    string Title,
    string Description,
    string? ImageUrl,
    string Url,
    string Type
);
