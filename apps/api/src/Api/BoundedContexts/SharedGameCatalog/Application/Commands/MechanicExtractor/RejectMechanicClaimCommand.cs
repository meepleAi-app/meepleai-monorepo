using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Rejects a single <c>MechanicClaim</c> with a mandatory note (ISSUE-584). The claim stays
/// attached to the analysis but cannot be published — the parent <c>Approve()</c> invariant
/// (all claims Approved) will fail until the claim is re-approved or fixed.
/// Valid only while the parent analysis is <c>InReview</c>.
/// </summary>
/// <param name="AnalysisId">Parent aggregate id.</param>
/// <param name="ClaimId">Claim id to reject.</param>
/// <param name="ReviewerId">Admin user id from the validated session.</param>
/// <param name="Note">Required rejection reason (1..500 chars).</param>
internal record RejectMechanicClaimCommand(
    Guid AnalysisId,
    Guid ClaimId,
    Guid ReviewerId,
    string Note) : ICommand<MechanicClaimDto>;
