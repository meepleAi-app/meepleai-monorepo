using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Approves every <c>Pending</c> claim on the given analysis in a single transaction
/// (ISSUE-584). <c>Approved</c> and <c>Rejected</c> claims are skipped — the operation is
/// idempotent on already-Approved claims and never overwrites a Rejected verdict (a Rejected
/// claim must be explicitly re-approved by the reviewer through the per-claim endpoint).
/// Valid only while the parent analysis is <c>InReview</c>; the domain guard surfaces as 409.
/// </summary>
/// <param name="AnalysisId">Parent aggregate id.</param>
/// <param name="ReviewerId">Admin user id from the validated session.</param>
internal record BulkApproveMechanicClaimsCommand(
    Guid AnalysisId,
    Guid ReviewerId) : ICommand<BulkApproveMechanicClaimsResponseDto>;
