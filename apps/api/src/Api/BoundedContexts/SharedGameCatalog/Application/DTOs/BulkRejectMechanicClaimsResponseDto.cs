namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Response payload for bulk reject (#526 ME-M1.4). Mirrors
/// <see cref="BulkApproveMechanicClaimsResponseDto"/>: carries the count of claims that
/// transitioned (for the toast) and the full updated claim list (so the admin UI re-renders
/// without a follow-up GET).
/// </summary>
/// <param name="RejectedCount">Number of claims that transitioned to <c>Rejected</c> in this
/// call. Claims already <c>Rejected</c> are not counted.</param>
/// <param name="SkippedAlreadyRejectedCount">Number of requested claims that were already
/// <c>Rejected</c> and left untouched (idempotent).</param>
/// <param name="Claims">The full claims list after the bulk operation, ordered by section then
/// display order. Lets the client refresh the table without a second round-trip.</param>
public sealed record BulkRejectMechanicClaimsResponseDto(
    int RejectedCount,
    int SkippedAlreadyRejectedCount,
    IReadOnlyList<MechanicClaimDto> Claims);
