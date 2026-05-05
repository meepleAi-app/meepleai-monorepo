using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Response DTO for the lifecycle state-change endpoints of the Mechanic Extractor
/// (ISSUE-524 / M1.2 follow-up, ADR-051). Covers <c>SubmitForReview</c>, <c>Approve</c> and
/// <c>Suppress</c> transitions — they all mutate the aggregate then return a small projection
/// so the admin UI can refresh in-place without polling the status endpoint.
/// </summary>
/// <param name="Id">Aggregate id (unchanged by all three operations).</param>
/// <param name="Status">New lifecycle status after the transition.</param>
/// <param name="ReviewedBy">Reviewer set by <c>Approve</c>. Remains <c>null</c> on
///   <c>SubmitForReview</c> and on <c>Suppress</c> (suppression is orthogonal to review).</param>
/// <param name="ReviewedAt">Companion timestamp to <paramref name="ReviewedBy"/>.</param>
/// <param name="IsSuppressed">Current suppression flag (T5 kill-switch). <c>true</c> after
///   <c>Suppress</c>, retains its previous value for the two lifecycle transitions.</param>
/// <param name="SuppressedAt">Timestamp set by <c>Suppress</c>.</param>
/// <param name="SuppressedBy">Admin who applied the takedown.</param>
/// <param name="SuppressionReason">Free-form reason captured for the suppression audit row.</param>
/// <param name="SuppressionRequestSource">Source channel of the takedown request (Email/Legal/Other).</param>
public sealed record MechanicAnalysisLifecycleResponseDto(
    Guid Id,
    MechanicAnalysisStatus Status,
    Guid? ReviewedBy,
    DateTime? ReviewedAt,
    bool IsSuppressed,
    DateTime? SuppressedAt,
    Guid? SuppressedBy,
    string? SuppressionReason,
    SuppressionRequestSource? SuppressionRequestSource);
