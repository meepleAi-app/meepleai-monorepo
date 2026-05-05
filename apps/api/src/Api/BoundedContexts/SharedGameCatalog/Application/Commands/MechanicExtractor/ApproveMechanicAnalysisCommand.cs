using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Transitions a <c>MechanicAnalysis</c> aggregate from <c>InReview</c> to <c>Published</c>
/// (ISSUE-524 / M1.2 follow-up, ADR-051). All claims must be <c>Approved</c> — the domain
/// invariant surfaces as <c>ConflictException</c> (409) when it is not satisfied.
/// </summary>
/// <param name="AnalysisId">Aggregate id to approve.</param>
/// <param name="ReviewerId">Admin user id sourced from the validated session (never from the body).</param>
internal record ApproveMechanicAnalysisCommand(Guid AnalysisId, Guid ReviewerId)
    : ICommand<MechanicAnalysisLifecycleResponseDto>;
