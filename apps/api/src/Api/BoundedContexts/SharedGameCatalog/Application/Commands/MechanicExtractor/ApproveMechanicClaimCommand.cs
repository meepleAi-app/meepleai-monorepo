using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Approves a single <c>MechanicClaim</c> belonging to a parent analysis (ISSUE-584).
/// Idempotent — re-approving an already-approved claim is a no-op. Valid only while the
/// parent analysis is <c>InReview</c>; the domain guard surfaces as 409.
/// </summary>
/// <param name="AnalysisId">Parent aggregate id.</param>
/// <param name="ClaimId">Claim id to approve.</param>
/// <param name="ReviewerId">Admin user id from the validated session (never from the body).</param>
/// <param name="Note">Optional free-form review note captured on approval (#526 AC-6, ≤ 2000 chars).</param>
internal record ApproveMechanicClaimCommand(
    Guid AnalysisId,
    Guid ClaimId,
    Guid ReviewerId,
    string? Note = null) : ICommand<MechanicClaimDto>;
