using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Rejects an explicit set of claims on the given analysis in a single transaction (#526
/// ME-M1.4). Unlike <see cref="BulkApproveMechanicClaimsCommand"/> — which approves every
/// <c>Pending</c> claim implicitly — bulk-reject takes an explicit <paramref name="ClaimIds"/>
/// list (decision D3): the admin UI computes the set client-side (e.g. all claims with a
/// &gt;20-word quote, or all T2-fail) and sends the ids, so the reviewer sees exactly what is
/// being rejected. Claims already <c>Rejected</c> are skipped (idempotent); a missing claim id
/// fails the whole batch with 404. Valid only while the parent analysis is <c>InReview</c>; the
/// domain guard surfaces as 409.
/// </summary>
/// <param name="AnalysisId">Parent aggregate id.</param>
/// <param name="ClaimIds">Explicit claim ids to reject (computed client-side).</param>
/// <param name="Reason">Shared rejection note applied to every rejected claim (1..500 chars).</param>
/// <param name="ReviewerId">Admin user id from the validated session.</param>
internal record BulkRejectMechanicClaimsCommand(
    Guid AnalysisId,
    IReadOnlyList<Guid> ClaimIds,
    string Reason,
    Guid ReviewerId) : ICommand<BulkRejectMechanicClaimsResponseDto>;
