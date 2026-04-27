using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

/// <summary>
/// Loads every <see cref="MechanicClaimDto"/> for an AI-generated analysis (ISSUE-584 claims viewer).
/// Powers the admin claims-review UI so reviewers can inspect, approve, or reject individual
/// claims before promoting the parent analysis to <c>Published</c> (AC-10).
/// Returns <c>null</c> when the parent analysis does not exist (router maps to 404).
/// </summary>
/// <param name="AnalysisId">Primary key of the parent <c>MechanicAnalysis</c> aggregate.</param>
internal sealed record GetMechanicAnalysisClaimsQuery(Guid AnalysisId)
    : IQuery<IReadOnlyList<MechanicClaimDto>?>;
