namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Response payload for bulk approve (ISSUE-584). Carries both the count of claims that
/// transitioned (so the toast can display "12 claims approved") and the full updated claim
/// list (so the admin UI can re-render without a follow-up GET).
/// </summary>
/// <param name="ApprovedCount">Number of claims that transitioned from Pending to Approved
/// in this call. Already-Approved claims are not counted; Rejected claims are skipped.</param>
/// <param name="SkippedRejectedCount">Number of <c>Rejected</c> claims that were left
/// untouched. Surfaced so the UI can warn the reviewer that those claims still block
/// promotion to <c>Published</c>.</param>
/// <param name="Claims">The full claims list after the bulk operation, ordered by section
/// then display order. Lets the client refresh the table without a second round-trip.</param>
public sealed record BulkApproveMechanicClaimsResponseDto(
    int ApprovedCount,
    int SkippedRejectedCount,
    IReadOnlyList<MechanicClaimDto> Claims);
