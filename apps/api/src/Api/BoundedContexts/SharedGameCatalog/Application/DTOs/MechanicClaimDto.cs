using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Read model for a single AI-generated <c>MechanicClaim</c> exposed to admin reviewers
/// (ISSUE-584 / claims viewer). Mirrors the per-claim shape needed by the admin UI to render
/// the list grouped by section, preview citations, and drive Approve/Reject actions.
/// </summary>
/// <param name="Id">Primary key of the claim.</param>
/// <param name="AnalysisId">FK to the parent <c>MechanicAnalysis</c> aggregate.</param>
/// <param name="Section">Logical rulebook section (Summary / Mechanics / Victory / …).</param>
/// <param name="Text">Player-facing rephrased rule text.</param>
/// <param name="DisplayOrder">0-based position inside the section.</param>
/// <param name="Status">Per-claim review status (Pending / Approved / Rejected).</param>
/// <param name="ReviewedBy">Admin who last reviewed the claim, or <c>null</c> if untouched.</param>
/// <param name="ReviewedAt">UTC timestamp of the last review action, or <c>null</c>.</param>
/// <param name="RejectionNote">Reason for rejection (set only when <see cref="Status"/> is Rejected).</param>
/// <param name="Citations">Attribution citations (≥ 1 — ADR-051 T3).</param>
public sealed record MechanicClaimDto(
    Guid Id,
    Guid AnalysisId,
    MechanicSection Section,
    string Text,
    int DisplayOrder,
    MechanicClaimStatus Status,
    Guid? ReviewedBy,
    DateTime? ReviewedAt,
    string? RejectionNote,
    IReadOnlyList<MechanicCitationDto> Citations);

/// <summary>
/// Read model for a single attribution <c>MechanicCitation</c>. Page + verbatim quote are the
/// minimal info the reviewer needs to verify a claim against the source PDF.
/// </summary>
/// <param name="Id">Primary key of the citation.</param>
/// <param name="PdfPage">1-based page number in the originating PDF.</param>
/// <param name="Quote">Verbatim quote from the rulebook (≤ 25 words — ADR-051 T1).</param>
/// <param name="DisplayOrder">0-based position inside the claim's citation list.</param>
public sealed record MechanicCitationDto(
    Guid Id,
    int PdfPage,
    string Quote,
    int DisplayOrder);
